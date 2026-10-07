using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AdNoctem.Substrate.Interop;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Tests.Fixtures;
using Xunit;

namespace AdNoctem.Substrate.Windows.Tests;

public sealed class OutlookWorkflowTests
{
    [Theory]
    [InlineData(OutlookFixture.E_ABORT, "0x80004004 (E_ABORT)")]
    [InlineData(0x08004004, "0x08004004")]
    public void OptionalContactFailureKeepsSafeMailAndSkipsUnclassifiedContactBranches(
        int hresult,
        string evidence
    )
    {
        var session = new OutlookFixture();
        var store = session.AddExisting(@"C:\Synthetic.pst", "optional-contact-store");
        var contacts = store.Standard[6].Folders.Add("Renamed contacts");
        contacts.DefaultItemType = 2;
        contacts.Folders.Add("NeverVisit");
        store.StandardErrors[30] = new COMException("Synthetic operation aborted.", hresult);
        var manager = new OutlookManager();
        var diagnostics = new List<string>();
        var created = store.FolderCreations;
        var identities = manager.GetStandardFolderIdentities(session, store.Root);
        var identity = identities.Single(item => item.Kind == OutlookFolderKind.SuggestedContacts);
        Assert.Equal(OutlookFolderIdentityState.Unresolved, identity.State);
        Assert.Contains(evidence, identity.Evidence);

        if (hresult != OutlookFixture.E_ABORT)
            Assert.DoesNotContain("E_ABORT", identity.Evidence);

        var plan = manager.GetFolderPlan(
            session,
            store.Root,
            recurse: true,
            diagnostic: diagnostics.Add
        );
        Assert.True(plan[0].Process);
        Assert.True(plan.Single(item => item.RelativePath == @"Posteingang\Keep").Process);
        var skipped = plan.Single(item => item.EntryId == contacts.EntryID);
        Assert.False(skipped.Process);
        Assert.False(skipped.Traverse);
        Assert.Equal("IncompleteIdentity:SuggestedContacts", skipped.Reason);
        Assert.DoesNotContain(
            plan,
            item => item.RelativePath == @"Posteingang\Renamed contacts\NeverVisit"
        );
        Assert.Contains(
            diagnostics,
            item =>
                item.Contains("SuggestedContacts")
                && item.Contains(evidence)
                && item.Contains(store.StoreID)
        );
        Assert.Contains(
            diagnostics,
            item => item.Contains("Skipped branch") && item.Contains(contacts.EntryID)
        );
        Assert.Equal(created, store.FolderCreations);
        Assert.Equal(0, store.ItemAccesses);
        Assert.Equal(0, session.Added);
        Assert.Equal(0, session.Removed);

        var included = manager.GetFolderPlan(
            session,
            store.Root,
            recurse: true,
            include: new[] { OutlookFolderKind.Inbox, OutlookFolderKind.SuggestedContacts }
        );
        Assert.True(
            included
                .Single(item => item.RelativePath == @"Posteingang\Renamed contacts\NeverVisit")
                .Process
        );
        var excluded = manager.GetFolderPlan(
            session,
            store.Root,
            recurse: true,
            include: new[] { OutlookFolderKind.Inbox, OutlookFolderKind.SuggestedContacts },
            exclusions: new[] { @"Posteingang\Renamed contacts" }
        );
        Assert.Equal(
            "CustomExclusion",
            excluded.Single(item => item.EntryId == contacts.EntryID).Reason
        );
    }

    [Theory]
    [InlineData(unchecked((int)0x8004010f), OutlookFolderIdentityState.Absent)]
    [InlineData(unchecked((int)0x80040102), OutlookFolderIdentityState.Unavailable)]
    [InlineData(unchecked((int)0x80070057), OutlookFolderIdentityState.Unavailable)]
    [InlineData(unchecked((int)0x80070005), OutlookFolderIdentityState.Unresolved)]
    public void DiscoveryDistinguishesAbsenceUnsupportedAndProviderFailure(
        int hresult,
        OutlookFolderIdentityState state
    )
    {
        var session = new OutlookFixture();
        var store = session.AddExisting(@"C:\Synthetic.pst", "discovery");
        store.StandardErrors[30] = new COMException("Synthetic lookup failure.", hresult);
        var diagnostics = new List<OutlookFolderIdentity>();
        var manager = new OutlookManager();
        var identities = manager.GetStandardFolderIdentities(
            session,
            store.Root,
            diagnostic: diagnostics.Add
        );
        Assert.Equal(
            state,
            identities.Single(item => item.Kind == OutlookFolderKind.SuggestedContacts).State
        );
        Assert.Contains(
            diagnostics,
            item =>
                item.Kind == OutlookFolderKind.SuggestedContacts
                && item.Evidence.Contains(hresult.ToString("X8"))
        );
        Assert.True(manager.GetFolderPlan(session, store.Root).Single().Process);
    }

    [Fact]
    public void UnknownMailIdentityDoesNotTurnProtectedBranchesIntoOrdinaryMail()
    {
        var session = new OutlookFixture();
        var store = session.AddExisting(@"C:\Synthetic.pst", "unknown-junk");
        var unknown = store.Root.Folders.Add("Renamed protected folder");
        unknown.Folders.Add("NeverVisit");
        store.StandardErrors[23] = new COMException(
            "Synthetic provider failure.",
            unchecked((int)0x80004005)
        );
        var diagnostics = new List<string>();
        var manager = new OutlookManager();
        var plan = manager.GetFolderPlan(
            session,
            store.Root,
            "",
            true,
            diagnostic: diagnostics.Add
        );
        Assert.False(plan[0].Process);
        Assert.True(plan[0].Traverse);
        Assert.True(plan.Single(item => item.EntryId == store.Standard[6].EntryID).Process);
        Assert.False(plan.Single(item => item.EntryId == store.Standard[3].EntryID).Traverse);
        Assert.Equal(
            "IncompleteIdentity:Junk",
            plan.Single(item => item.EntryId == unknown.EntryID).Reason
        );
        Assert.DoesNotContain(plan, item => item.EntryId == unknown.Folders.Values[0].EntryID);
        Assert.Contains(diagnostics, item => item.Contains("Junk") && item.Contains("0x80004005"));
        var included = manager.GetFolderPlan(
            session,
            store.Root,
            "",
            true,
            new[] { OutlookFolderKind.Inbox, OutlookFolderKind.Junk }
        );
        Assert.True(included.Single(item => item.EntryId == unknown.EntryID).Process);
        Assert.Equal(
            "SearchFolder",
            included.Single(item => item.RelativePath == @"Posteingang\Search").Reason
        );
    }

    [Theory]
    [InlineData("16.0")]
    [InlineData("12.0")]
    public void InboxIsRequiredOnlyForImplicitSelection(string version)
    {
        var session = new OutlookFixture();
        session.Application.Version = version;
        var store = session.AddExisting(@"C:\Synthetic.pst", "archive-without-inbox");
        store.Root.Folders.Values.Remove(store.Standard[6]);
        store.Standard.Remove(6);
        var archive = store.Root.Folders.Add("Existing archive");
        var manager = new OutlookManager();
        var failure = Assert.Throws<InvalidOperationException>(() =>
            manager.GetFolderPlan(session, store.Root)
        );
        Assert.Contains("Inbox identity", failure.Message);
        Assert.Contains(store.StoreID, failure.Message);
        Assert.True(
            manager.GetFolderPlan(session, store.Root, "Existing archive").Single().Process
        );
        Assert.Contains(
            manager.GetFolderPlan(session, store.Root, "", true),
            item => item.EntryId == archive.EntryID && item.Process
        );
        Assert.Throws<InvalidOperationException>(() =>
            manager.GetFolderPlan(session, store.Root, "Does not exist")
        );
        store.StandardErrors[6] = new COMException(
            "Synthetic Inbox access denied.",
            unchecked((int)0x80070005)
        );
        failure = Assert.Throws<InvalidOperationException>(() =>
            manager.GetFolderPlan(session, store.Root)
        );
        Assert.Contains("0x80070005", failure.Message);
        Assert.True(
            manager.GetFolderPlan(session, store.Root, "Existing archive").Single().Process
        );
    }

    [Fact]
    public void LegacyPropertyFailuresRemainUnknownAndCanBeResolvedByAnotherAccessor()
    {
        var session = new OutlookFixture();
        session.Application.Version = "12.0";
        var store = session.AddExisting(@"C:\Synthetic.pst", "legacy-provider");
        const string tag = "http://schemas.microsoft.com/mapi/proptag/0x36D10102";
        var contacts = store.Standard[6].Folders.Add("Renamed contacts");
        contacts.DefaultItemType = 2;
        contacts.EntryID = "ABCD";
        contacts.Folders.Add("NeverVisit");
        store.Standard[6].PropertyAccessor.Errors[tag] = new COMException(
            "Synthetic provider failure.",
            OutlookFixture.E_ABORT
        );
        store.Root.PropertyAccessor.Errors[tag] = new COMException(
            "Synthetic unsupported property.",
            unchecked((int)0x80040102)
        );
        var manager = new OutlookManager();
        var diagnostics = new List<string>();
        var plan = manager.GetFolderPlan(
            session,
            store.Root,
            recurse: true,
            diagnostic: diagnostics.Add
        );
        Assert.True(plan[0].Process);
        Assert.Equal(
            "IncompleteIdentity:Contacts",
            plan.Single(item => item.EntryId == contacts.EntryID).Reason
        );
        Assert.Contains(
            diagnostics,
            item => item.Contains("Contacts") && item.Contains("0x80004004")
        );
        store.PropertyAccessor.Properties[tag] = new byte[] { 0xAB, 0xCD };
        var identity = manager
            .GetStandardFolderIdentities(session, store.Root)
            .Single(item => item.Kind == OutlookFolderKind.Contacts);
        Assert.Equal(OutlookFolderIdentityState.Resolved, identity.State);
        Assert.Equal(contacts.EntryID, identity.EntryId);
        Assert.StartsWith(
            "StandardFolder:Contacts:",
            manager
                .GetFolderPlan(session, store.Root, recurse: true)
                .Single(item => item.EntryId == contacts.EntryID)
                .Reason
        );
        store.PropertyAccessor.Properties[tag] = "invalid binary identity";
        Assert.Throws<InvalidDataException>(() => manager.GetFolderPlan(session, store.Root));
    }

    [Fact]
    public void SourceInspectionInvalidIdentitiesAndCancellationRemainFailures()
    {
        var session = new OutlookFixture();
        var store = session.AddExisting(@"C:\Synthetic.pst", "source-errors");
        store.StandardErrors[30] = new OperationCanceledException();
        var manager = new OutlookManager();
        Assert.Throws<OperationCanceledException>(() => manager.GetFolderPlan(session, store.Root));
        store.StandardErrors.Clear();
        var wrong = session.AddExisting(@"C:\Wrong.pst", "wrong");
        store.Standard[30] = wrong.Root;
        Assert.Throws<InvalidOperationException>(() => manager.GetFolderPlan(session, store.Root));
        store.Standard.Remove(30);
        store.Standard[6].PropertyAccessor.Errors[
            "http://schemas.microsoft.com/mapi/proptag/0x36010003"
        ] = new COMException("Source access failed.", unchecked((int)0x80070005));
        Assert.Throws<COMException>(() => manager.GetFolderPlan(session, store.Root));
    }

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
