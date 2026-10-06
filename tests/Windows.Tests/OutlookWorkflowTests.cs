using System;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.Interop;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Tests.Fixtures;
using Xunit;

namespace AdNoctem.Substrate.Windows.Tests;

public sealed class OutlookWorkflowTests
{
    [Fact]
    public void FolderPlanningUsesIdentityAndCompleteTraversalBeforeReturning()
    {
        var session = new OutlookFixture();
        var store = session.AddExisting(@"C:\Synthetic.pst", "synthetic");
        var manager = new OutlookManager();
        var plan = manager.GetFolderPlan(
            session,
            store.Root,
            recurse: true,
            exclusions: new[] { @"Posteingang\Skip" }
        );
        Assert.Equal("Posteingang", plan[0].RelativePath);
        Assert.True(plan[0].Process);
        Assert.DoesNotContain(
            plan,
            item => item.RelativePath.EndsWith("NeverVisit", StringComparison.Ordinal)
        );
        Assert.Equal(
            "SearchFolder",
            plan.Single(item =>
                item.RelativePath.EndsWith("Search", StringComparison.Ordinal)
            ).Reason
        );
        Assert.True(
            plan.Single(item =>
                item.RelativePath.EndsWith("MailChild", StringComparison.Ordinal)
            ).Process
        );
        Assert.Equal(
            "NonMailContainer",
            plan.Single(item =>
                item.RelativePath.EndsWith("CalendarContainer", StringComparison.Ordinal)
            ).Reason
        );
        Assert.Throws<InvalidOperationException>(() =>
            manager.GetFolderPlan(session, store.Root, "Gelöscht")
        );
        Assert.Throws<OperationCanceledException>(() =>
            manager.GetFolderPlan(
                session,
                store.Root,
                cancellationToken: new CancellationToken(true)
            )
        );
        store.Standard[6].Folders.Values.Add(store.Standard[6]);
        Assert.Throws<InvalidOperationException>(() =>
            manager.GetFolderPlan(session, store.Root, recurse: true)
        );
        var identities = OutlookManager.ParseAdditionalFolderEntryIds(
            new byte[] { 1, 128, 6, 0, 1, 0, 2, 0, 10, 11 }
        );
        Assert.Equal(new byte[] { 10, 11 }, identities[OutlookFolderKind.RssFeeds]);
        Assert.Throws<InvalidDataException>(() =>
            OutlookManager.ParseAdditionalFolderEntryIds(
                new byte[] { 1, 128, 7, 0, 1, 0, 2, 0, 10, 11 }
            )
        );
        Assert.Equal(
            "<abc@example.test>",
            MailHeaderParser.GetTransportMessageId(
                "Received: elsewhere\r\nMessage-ID: <abc@example.test>\r\n"
            )
        );
    }

    [Fact]
    public void PstLifetimesDetachOnlyOwnedAttachmentsAndRecoverFailedAttach()
    {
        var path = Path.Combine(Path.GetTempPath(), "psf-" + Guid.NewGuid().ToString("N") + ".pst");
        File.WriteAllText(path, "synthetic");

        try
        {
            var file = FileSystemPath.Parse(path);
            var session = new OutlookFixture();
            var manager = new OutlookManager();
            Assert.Null(manager.OpenPstStore(session, file, false));
            Assert.Equal(0, session.Added);
            var owned = manager.OpenPstStore(session, file)!;
            Assert.True(owned.AttachedByCall);

            using (var borrowed = manager.OpenPstStore(session, file)!)
                Assert.False(borrowed.AttachedByCall);

            Assert.Equal(0, session.Removed);
            owned.Dispose();
            owned.Dispose();
            Assert.True(owned.Closed);
            Assert.Null(owned.Root);
            Assert.Equal(1, session.Removed);
            session.FailAfterAttach = true;
            Assert.Throws<InvalidOperationException>(() => manager.OpenPstStore(session, file));
            Assert.Empty(session.Stores.Values);
            Assert.Equal(2, session.Removed);
            session.FailAfterAttach = false;
            var replacement = manager.OpenPstStore(session, file)!;
            session.Stores.Values.Clear();
            session.AddExisting(path, "replacement");
            replacement.Dispose();
            Assert.Equal("replacement", Assert.Single(session.Stores.Values).StoreID);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
