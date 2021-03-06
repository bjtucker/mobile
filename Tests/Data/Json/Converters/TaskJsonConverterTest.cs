﻿using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class TaskJsonConverterTest : Test
    {
        private TaskJsonConverter converter;

        public override void SetUp ()
        {
            base.SetUp ();

            converter = new TaskJsonConverter ();
        }

        [Test]
        public void ExportExisting ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = Guid.NewGuid (),
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "Install Linux",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 3),
                });

                var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, taskData));
                Assert.AreEqual (2, json.Id);
                Assert.AreEqual ("Install Linux", json.Name);
                Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
                Assert.AreEqual (1, json.WorkspaceId);
                Assert.AreEqual (3, json.ProjectId);
                Assert.IsNull (json.DeletedAt);
            });
        }

        [Test]
        public void ExportInvalidWorkspaceAndProject ()
        {
            TaskData taskData = null;

            RunAsync (async delegate {
                taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "Install Linux",
                    WorkspaceId = Guid.NewGuid (),
                    ProjectId = Guid.NewGuid (),
                    ModifiedAt = new DateTime (2014, 1, 3),
                });
            });

            Assert.That (() => RunAsync (async delegate {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, taskData));
            }), Throws.Exception.TypeOf<RelationRemoteIdMissingException> ());
        }

        [Test]
        public void ExportNew ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = Guid.NewGuid (),
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    Name = "Install Linux",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 3),
                });

                var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, taskData));
                Assert.IsNull (json.Id);
                Assert.AreEqual ("Install Linux", json.Name);
                Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
                Assert.AreEqual (1, json.WorkspaceId);
                Assert.AreEqual (3, json.ProjectId);
                Assert.IsNull (json.DeletedAt);
            });
        }

        [Test]
        public void ImportNew ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 3,
                    ModifiedAt = new DateTime (2014, 1, 3),
                };

                var taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreNotEqual (Guid.Empty, taskData.Id);
                Assert.AreEqual (2, taskData.RemoteId);
                Assert.AreEqual ("Install Linux", taskData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 3), taskData.ModifiedAt);
                Assert.AreEqual (workspaceData.Id, taskData.WorkspaceId);
                Assert.AreEqual (projectData.Id, taskData.ProjectId);
                Assert.IsFalse (taskData.IsDirty);
                Assert.IsFalse (taskData.RemoteRejected);
                Assert.IsNull (taskData.DeletedAt);
            });
        }

        [Test]
        public void ImportUpdated ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                });
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 3,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
                };

                taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreNotEqual (Guid.Empty, taskData.Id);
                Assert.AreEqual (2, taskData.RemoteId);
                Assert.AreEqual ("Install Linux", taskData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), taskData.ModifiedAt);
                Assert.AreEqual (workspaceData.Id, taskData.WorkspaceId);
                Assert.AreEqual (projectData.Id, taskData.ProjectId);
                Assert.IsFalse (taskData.IsDirty);
                Assert.IsFalse (taskData.RemoteRejected);
                Assert.IsNull (taskData.DeletedAt);
            });

            // Warn the user that the test result might be invalid
            if (TimeZone.CurrentTimeZone.GetUtcOffset (DateTime.Now).TotalMinutes >= 0) {
                Assert.Inconclusive ("The test machine timezone should be set to GTM-1 or less to test datetime comparison.");
            }
        }

        [Test]
        [Description ("Overwrite local non-dirty data regardless of the modification times.")]
        public void ImportUpdatedOverwriteNonDirtyLocal ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                });
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 3,
                    ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
                };

                taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreEqual ("Install Linux", taskData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), taskData.ModifiedAt);
            });
        }

        [Test]
        [Description ("Overwrite dirty local data if imported data has a modification time greater than local.")]
        public void ImportUpdatedOverwriteDirtyLocal ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                    IsDirty = true,
                });
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 3,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
                };

                taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreEqual ("Install Linux", taskData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), taskData.ModifiedAt);
            });
        }

        [Test]
        [Description ("Overwrite local dirty-but-rejected data regardless of the modification times.")]
        public void ImportUpdatedOverwriteRejectedLocal ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                    IsDirty = true,
                    RemoteRejected = true,
                });
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 3,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
                };

                taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreEqual ("Install Linux", taskData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), taskData.ModifiedAt);
            });
        }

        [Test]
        [Description ("Keep local dirty data when imported data has same or older modification time.")]
        public void ImportUpdatedKeepDirtyLocal ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                    IsDirty = true,
                });
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 3,
                    ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
                };

                taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreEqual ("", taskData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), taskData.ModifiedAt);
            });
        }

        [Test]
        public void ImportMissingWorkspaceAndProject ()
        {
            RunAsync (async delegate {
                var taskJson = new TaskJson () {
                    Id = 2,
                    Name = "Install Linux",
                    WorkspaceId = 1,
                    ProjectId = 2,
                    ModifiedAt = new DateTime (2014, 1, 3),
                };

                var taskData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.AreNotEqual (Guid.Empty, taskData.WorkspaceId);

                var workspaceRows = await DataStore.Table<WorkspaceData> ().QueryAsync (m => m.Id == taskData.WorkspaceId);
                var workspaceData = workspaceRows.FirstOrDefault ();
                Assert.IsNotNull (workspaceData);
                Assert.IsNotNull (workspaceData.RemoteId);
                Assert.AreEqual (DateTime.MinValue, workspaceData.ModifiedAt);

                var projectRows = await DataStore.Table<ProjectData> ().QueryAsync (m => m.Id == taskData.ProjectId);
                var projectData = projectRows.FirstOrDefault ();
                Assert.IsNotNull (projectData);
                Assert.IsNotNull (projectData.RemoteId);
                Assert.AreEqual (DateTime.MinValue, projectData.ModifiedAt);
            });
        }

        [Test]
        public void ImportDeleted ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = Guid.NewGuid (),
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "Install Linux",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 3),
                });

                var taskJson = new TaskJson () {
                    Id = 2,
                    DeletedAt = new DateTime (2014, 1, 4),
                };

                var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.IsNull (ret);

                var rows = await DataStore.Table<TaskData> ().QueryAsync (m => m.Id == taskData.Id);
                Assert.That (rows, Has.Exactly (0).Count);
            });
        }

        [Test]
        public void ImportPastDeleted ()
        {
            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    WorkspaceId = Guid.NewGuid (),
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var taskData = await DataStore.PutAsync (new TaskData () {
                    RemoteId = 2,
                    Name = "Install Linux",
                    WorkspaceId = workspaceData.Id,
                    ProjectId = projectData.Id,
                    ModifiedAt = new DateTime (2014, 1, 3),
                });

                var taskJson = new TaskJson () {
                    Id = 2,
                    DeletedAt = new DateTime (2014, 1, 2),
                };

                var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, taskJson));
                Assert.IsNull (ret);

                var rows = await DataStore.Table<TaskData> ().QueryAsync (m => m.Id == taskData.Id);
                Assert.That (rows, Has.Exactly (0).Count);
            });
        }
    }
}
