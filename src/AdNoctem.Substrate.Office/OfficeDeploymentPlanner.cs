using System;
using System.Collections.Generic;
using System.Linq;

namespace AdNoctem.Substrate.Office;

/// <summary>Pure planning over explicit observations. No I/O, user interaction, elevation or execution authority.</summary>
public sealed class OfficeDeploymentPlanner
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;
    public OfficeDeploymentPlan Create(OfficeDeploymentRequest request, OfficeInventory inventory, OfficePlanningMedia? media = null)
        => Create(request, OfficePlanningInventory.FromInventory(inventory), media);
    public OfficeDeploymentPlan Create(OfficeDeploymentRequest request, OfficePlanningInventory inventory, OfficePlanningMedia? media = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        var target = request.Configuration;
        var action = request.Action;
        var products = inventory.Products;
        var msi = inventory.Msi;
        var selection = request.RemoveProductIds;
        var blockers = new List<string>();
        var warnings = new List<string>();
        var state = inventory.Unknowns.Count != 0 ? OfficeDeploymentState.Unknown : products.Count != 0 || msi.Count != 0 ? OfficeDeploymentState.Conflict : OfficeDeploymentState.Clean;
        if (inventory.Unknowns.Count != 0)
            blockers.Add("UnknownInventory");
        OfficeDeploymentAssessment? verification = null;
        if (target != null)
        {
            if (!string.IsNullOrEmpty(request.SourcePath))
            {
                if (media == null)
                    blockers.Add("UnverifiedMedia");
                else if (!media.Valid)
                    blockers.Add(media.ReasonCode!);
                else if (target.Version == null && media.Version != null)
                    target = new OfficeConfiguration(target.Product, target.Architecture, target.Channel, target.Languages, media.Version, target.ExcludedApplications);
            }
            verification = new OfficeDeploymentValidator().Evaluate(target, products, msi.Count != 0, inventory.Unknowns);
            if (verification.Compliant)
                state = OfficeDeploymentState.Compliant;
            else if (products.Any(product => Names.Equals(product.ProductId, target.Product.ToString())) && verification.Discrepancies.Count == 0)
                state = OfficeDeploymentState.Incomplete;
        }
        if (action == OfficeDeploymentAction.Install)
        {
            if (state == OfficeDeploymentState.Incomplete)
                blockers.Add("RecoveryRequired");
            else if (state != OfficeDeploymentState.Clean && state != OfficeDeploymentState.Compliant)
                blockers.Add("Conflict");
        }
        if (action == OfficeDeploymentAction.Remove)
        {
            if (request.RemoveMsi)
                blockers.Add("UnsupportedStandaloneMsi");
            if (products.Any(product => selection.Contains(product.ProductId, Names)) && products.Where(product => !selection.Contains(product.ProductId, Names))
                .Any(product => product.Languages == null || string.IsNullOrEmpty(product.PrimaryLanguage) || product.ExcludedApplications == null))
                blockers.Add("UnsupportedSharedComponentVerification");
        }
        if (action == OfficeDeploymentAction.Migrate)
        {
            if (state != OfficeDeploymentState.Compliant && products.Any(product => !selection.Contains(product.ProductId, Names)))
                blockers.Add("UnapprovedProducts");
            if (state != OfficeDeploymentState.Compliant && selection.Any(id => !products.Any(product => Names.Equals(product.ProductId, id))))
                blockers.Add("StaleRemovalSelection");
            if (msi.Count != 0 && !request.RemoveMsi)
                blockers.Add("MsiConsentRequired");
            if (msi.Any(product => !OfficeVersionResolver.IsMatch(product.Version, @"^(12|14|15|16)\.") || OfficeVersionResolver.IsMatch(product.Name, "Lync.*2010|Access.*(Database Engine|Runtime).*2007")))
                blockers.Add("UnsupportedMsiComponent");
        }
        var maintenance = action != OfficeDeploymentAction.Install && action != OfficeDeploymentAction.Remove && action != OfficeDeploymentAction.Migrate;
        if (maintenance)
        {
            if (msi.Count != 0 || products.Count != 1 || !Names.Equals(products[0].ProductId, target!.Product.ToString()))
                blockers.Add("Conflict");
            else
            {
                var allowed = action == OfficeDeploymentAction.Update ? "Version" : action == OfficeDeploymentAction.SetApplicationSelection ? "ExcludeApp"
                    : action == OfficeDeploymentAction.AddLanguage || action == OfficeDeploymentAction.RemoveLanguage ? "Languages" : null;
                if (verification!.Unknowns.Count != 0 || verification.Discrepancies.Any(field => !Names.Equals(field, allowed)))
                    blockers.Add("UnverifiedPreservedConfiguration");
                var current = products[0];
                if (action == OfficeDeploymentAction.Update && target!.Version != null && Version.TryParse(current.Version, out var currentVersion) && target.Version < currentVersion)
                    blockers.Add("DowngradeRequiresMigration");
                if (action == OfficeDeploymentAction.AddLanguage || action == OfficeDeploymentAction.RemoveLanguage)
                {
                    IEnumerable<string> expected = current.Languages ?? Array.Empty<string>();
                    if (action == OfficeDeploymentAction.AddLanguage)
                        expected = expected.Concat(request.Languages);
                    else
                    {
                        expected = expected.Except(request.Languages, Names).ToArray();
                        if (request.Languages.Contains(current.PrimaryLanguage ?? "", Names) || !expected.Any())
                            blockers.Add("PrimaryLanguageRequiresMigration");
                    }
                    if (!Sorted(expected).SequenceEqual(Sorted(target!.Languages), Names))
                        blockers.Add("LanguageSelectionMismatch");
                }
            }
        }
        var needsMedia = (action == OfficeDeploymentAction.Install || action == OfficeDeploymentAction.Migrate || action == OfficeDeploymentAction.Update
            || action == OfficeDeploymentAction.AddLanguage || action == OfficeDeploymentAction.SetApplicationSelection) && state != OfficeDeploymentState.Compliant;
        if ((action == OfficeDeploymentAction.Install || action == OfficeDeploymentAction.Migrate) && state != OfficeDeploymentState.Compliant && inventory.VerificationLimitations.Any(value => !string.IsNullOrEmpty(value)))
        {
            blockers.Add("UnsupportedNativeVerification");
            warnings.Add("The native inventory backend cannot verify: " + string.Join(", ", inventory.VerificationLimitations) + ". No deployment may start until these postconditions can be verified.");
        }
        if (needsMedia && string.IsNullOrEmpty(request.SourcePath))
            blockers.Add("MissingMedia");
        var before = Sorted(products.SelectMany(product => product.Languages ?? Array.Empty<string>()).Where(value => !string.IsNullOrEmpty(value)));
        var primaries = Sorted(products.Select(product => product.PrimaryLanguage).Where(value => !string.IsNullOrEmpty(value)).Cast<string>());
        var known = msi.Count == 0 && inventory.Unknowns.Count == 0 && (products.Count == 0 || primaries.Length == 1 && products.All(product =>
            product.Languages != null && product.Languages.Count != 0 && !string.IsNullOrEmpty(product.PrimaryLanguage) && product.Languages.Contains(product.PrimaryLanguage!, Names)));
        OfficeLanguageTransition? transition = null;
        if (target != null)
        {
            transition = new OfficeLanguageTransition(known, before, target, primaries);
            if (!known)
                warnings.Add("Source languages/primary language are unknown; preservation cannot be established.");
            else if ((products.Count != 0 || msi.Count != 0) && (transition.Added.Count != 0 || transition.Removed.Count != 0 || !Names.Equals(string.Join(",", primaries), target.PrimaryLanguage)))
                warnings.Add("Language change: " + string.Join(",", before) + " -> " + string.Join(",", target.Languages) + "; removed [" + string.Join(",", transition.Removed) + "]; primary -> " + target.PrimaryLanguage + ".");
        }
        if (action == OfficeDeploymentAction.SetUpdateConfiguration)
            warnings.Add("Update settings can cause future downloads/build or channel transitions. Managed policy may override them.");
        if (action == OfficeDeploymentAction.SetApplicationPreference)
            warnings.Add("Application preferences affect existing and future users on this machine.");
        return new OfficeDeploymentPlan(request, inventory, target, state, blockers, warnings, transition, media);
    }
    private static string[] Sorted(IEnumerable<string> values) => values.Distinct(Names).OrderBy(value => value, Names).ToArray();
}
