using System;
using System.Collections.Generic;
using System.Linq;

namespace AdNoctem.Substrate.Office;

/// <summary>Checks deployment postconditions, including preservation of registrations outside the selected removal scope.</summary>
public sealed class OfficeDeploymentVerification
{
    public OfficeDeploymentAssessment Evaluate(
        OfficeDeploymentPlanDocument plan,
        OfficeInventory after
    )
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        var current = OfficeInventorySerializer.Document(after);
        var before = plan.Data.Object("Before");
        var priorAddIns = before.Has("RelatedComponents")
            ? Rows(before, "RelatedComponents")
                .Where(item => OfficeDocument.Equal(item.Text("Role"), "AddIn"))
            : Enumerable.Empty<OfficeDocument>();

        foreach (var item in priorAddIns)
        {
            var matches = Rows(current, "RelatedComponents")
                .Where(value =>
                    OfficeDocument.Equal(value.Text("ProductCode"), item.Text("ProductCode"))
                    && OfficeDocument.Equal(value.Text("RegistryView"), item.Text("RegistryView"))
                )
                .ToArray();

            if (
                matches.Length != 1
                || OfficeDocument.Fingerprint(matches[0]) != OfficeDocument.Fingerprint(item)
            )
                return Assessment(new[] { "RelatedAddInChanged" });
        }

        if (plan.Request.Action == OfficeDeploymentAction.Remove)
        {
            var previous = Rows(before, "Products").ToArray();
            var observed = Rows(current, "Products").ToArray();
            var selected = plan.Request.RemoveProductIds;
            var retained = previous
                .Where(item =>
                    !selected.Contains(item.Text("ProductId")!, StringComparer.OrdinalIgnoreCase)
                )
                .ToArray();
            var missing = retained
                .Where(item =>
                    !observed.Any(value =>
                        OfficeDocument.Equal(value.Text("ProductId"), item.Text("ProductId"))
                    )
                )
                .ToArray();
            var unexpected = observed
                .Where(item =>
                    !previous.Any(value =>
                        OfficeDocument.Equal(value.Text("ProductId"), item.Text("ProductId"))
                    )
                )
                .ToArray();
            var changed = retained
                .Where(item =>
                {
                    var matches = observed
                        .Where(value =>
                            OfficeDocument.Equal(value.Text("ProductId"), item.Text("ProductId"))
                        )
                        .ToArray();

                    return matches.Length != 1
                        || OfficeDocument.Fingerprint(matches[0])
                            != OfficeDocument.Fingerprint(item);
                })
                .ToArray();
            var differences = missing
                .Concat(changed)
                .Select(item => item.Text("ProductId")!)
                .Concat(unexpected.Select(item => "UnexpectedProduct:" + item.Text("ProductId")))
                .ToList();

            // The legacy report omitted these discrepancy names but they must still prevent a compliant native assessment.
            if (
                observed.Any(item =>
                    selected.Contains(item.Text("ProductId")!, StringComparer.OrdinalIgnoreCase)
                )
            )
                differences.Add("SelectedProductsRemain");

            if (
                OfficeDocument.Fingerprint(before["Msi"])
                != OfficeDocument.Fingerprint(current["Msi"])
            )
                differences.Add("MsiChanged");

            return new OfficeDeploymentAssessment(differences, after.Unknowns.ToList());
        }

        if (
            plan.Request.Action == OfficeDeploymentAction.SetUpdateConfiguration
            || plan.Request.Action == OfficeDeploymentAction.SetApplicationPreference
        )
            return Assessment(Array.Empty<string>(), new[] { "EffectiveSettings" });

        return AssessTarget(plan, after);
    }

    internal static OfficeDeploymentAssessment AssessTarget(
        OfficeDeploymentPlanDocument plan,
        OfficeInventory current
    )
    {
        var assessment = new OfficeDeploymentValidator().Evaluate(
            plan.Request.Configuration!,
            current
        );
        var differences = assessment.Discrepancies.ToList();
        var version = plan.Data.Object("Configuration").Text("Version");
        var selected = current
            .Products.Where(value =>
                OfficeDocument.Equal(
                    value.ProductId,
                    plan.Request.Configuration!.Product.ToString()
                )
            )
            .ToArray();

        if (selected.Length == 1 && !string.IsNullOrEmpty(version) && selected[0].Version != null)
        {
            differences.Remove("Version");

            if (!OfficeDocument.Equal(version, selected[0].Version!.ToString()))
            {
                var position = differences.FindIndex(value =>
                    value == "PrimaryLanguage"
                    || value == "Languages"
                    || value == "ExcludeApp"
                    || value == "OtherProducts"
                );
                differences.Insert(position < 0 ? differences.Count : position, "Version");
            }
        }

        return new OfficeDeploymentAssessment(differences, assessment.Unknowns.ToList());
    }

    internal static IEnumerable<OfficeDocument> Rows(OfficeDocument document, string name) =>
        document
            .Array(name)
            .Select(item => item as OfficeDocument ?? throw OfficeDocument.Invalid());

    private static OfficeDeploymentAssessment Assessment(
        IEnumerable<string> differences,
        IEnumerable<string>? unknowns = null
    ) =>
        new OfficeDeploymentAssessment(
            differences.ToList(),
            (unknowns ?? Array.Empty<string>()).ToList()
        );
}
