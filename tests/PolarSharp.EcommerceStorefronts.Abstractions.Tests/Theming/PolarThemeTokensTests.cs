using System.Linq;
using PolarSharp.EcommerceStorefronts.Abstractions.Theming;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Tests.Theming;

/// <summary>
/// Contract tests for <see cref="PolarThemeTokens"/>. These guard the registry's invariants
/// (token count per category, attribute-alias tally, naming prefix) so accidental edits to
/// the static descriptor list fail loudly instead of silently shifting the agent-consumable
/// theme contract.
/// </summary>
public sealed class PolarThemeTokensTests
{
    [Fact]
    public void AllTokens_count_matches_PLAN_md_tally()
    {
        // PLAN.md v1.4.0 Phase 3 token catalog:
        //   12 colors + 6 typography + 3 spacing + 3 shape + 2 motion
        // + 4 component-specific + 1 semantic size-scale = 31 tokens.
        Assert.Equal(31, PolarThemeTokens.AllTokens.Count);
    }

    [Fact]
    public void Tokens_by_category_sum_to_total()
    {
        var colors = PolarThemeTokens.ByCategory(ThemeTokenCategory.Colors).Count;
        var typography = PolarThemeTokens.ByCategory(ThemeTokenCategory.Typography).Count;
        var spacing = PolarThemeTokens.ByCategory(ThemeTokenCategory.Spacing).Count;
        var shape = PolarThemeTokens.ByCategory(ThemeTokenCategory.Shape).Count;
        var motion = PolarThemeTokens.ByCategory(ThemeTokenCategory.Motion).Count;
        var componentSpecific = PolarThemeTokens.ByCategory(ThemeTokenCategory.ComponentSpecific).Count;

        Assert.Equal(12, colors);
        Assert.Equal(6, typography);
        Assert.Equal(3, spacing);
        Assert.Equal(3, shape);
        Assert.Equal(2, motion);
        // 4 component-specific (image-aspect-ratio, mini-cart-position, toast-position,
        // toast-duration) + the 1 size-scale token = 5.
        Assert.Equal(5, componentSpecific);

        Assert.Equal(
            PolarThemeTokens.AllTokens.Count,
            colors + typography + spacing + shape + motion + componentSpecific);
    }

    [Fact]
    public void AttributeAliased_count_is_8()
    {
        // 8 tokens are attribute-aliased per PLAN.md:
        //   primary, accent, border, border-radius, product-card image-aspect-ratio,
        //   mini-cart position, toast position, toast-duration.
        Assert.Equal(8, PolarThemeTokens.AttributeAliased.Count);
    }

    [Fact]
    public void AttributeAliased_contains_expected_tokens()
    {
        var names = PolarThemeTokens.AttributeAliased.Select(t => t.Name).ToHashSet();

        Assert.Contains(PolarThemeTokens.Primary, names);
        Assert.Contains(PolarThemeTokens.Accent, names);
        Assert.Contains(PolarThemeTokens.Border, names);
        Assert.Contains(PolarThemeTokens.BorderRadius, names);
        Assert.Contains(PolarThemeTokens.ProductCardImageAspectRatio, names);
        Assert.Contains(PolarThemeTokens.MiniCartPosition, names);
        Assert.Contains(PolarThemeTokens.ToastPosition, names);
        Assert.Contains(PolarThemeTokens.ToastDuration, names);
    }

    [Fact]
    public void Every_attribute_aliased_token_has_non_null_alias_name()
    {
        foreach (var token in PolarThemeTokens.AttributeAliased)
        {
            Assert.True(token.HasAttributeAlias, $"{token.Name} appears in AttributeAliased but HasAttributeAlias is false");
            Assert.False(
                string.IsNullOrWhiteSpace(token.AttributeAliasName),
                $"{token.Name} is attribute-aliased but AttributeAliasName is null/empty");
        }
    }

    [Fact]
    public void Every_css_variable_only_token_has_null_alias_name()
    {
        foreach (var token in PolarThemeTokens.CssVariableOnly)
        {
            Assert.False(token.HasAttributeAlias, $"{token.Name} appears in CssVariableOnly but HasAttributeAlias is true");
            Assert.Null(token.AttributeAliasName);
        }
    }

    [Fact]
    public void CssVariableOnly_and_AttributeAliased_partition_the_registry()
    {
        var aliased = PolarThemeTokens.AttributeAliased.Count;
        var cssOnly = PolarThemeTokens.CssVariableOnly.Count;

        Assert.Equal(PolarThemeTokens.AllTokens.Count, aliased + cssOnly);
        // 23 CSS-variable-only per PLAN.md (22 originally + the new size-scale).
        Assert.Equal(23, cssOnly);
    }

    [Fact]
    public void Every_token_name_starts_with_polar_prefix()
    {
        foreach (var (name, descriptor) in PolarThemeTokens.AllTokens)
        {
            Assert.StartsWith("--polar-", name);
            Assert.Equal(name, descriptor.Name);
        }
    }

    [Fact]
    public void Token_names_are_unique()
    {
        var distinct = PolarThemeTokens.AllTokens.Keys.Distinct().Count();
        Assert.Equal(PolarThemeTokens.AllTokens.Count, distinct);
    }

    [Fact]
    public void Const_string_members_are_present_in_registry()
    {
        // Spot-check that the public const members agents reference are actually keys
        // in AllTokens — guards against a typo between the const declaration + the
        // descriptor row.
        Assert.True(PolarThemeTokens.AllTokens.ContainsKey(PolarThemeTokens.Primary));
        Assert.True(PolarThemeTokens.AllTokens.ContainsKey(PolarThemeTokens.SizeScale));
        Assert.True(PolarThemeTokens.AllTokens.ContainsKey(PolarThemeTokens.ToastDuration));
        Assert.True(PolarThemeTokens.AllTokens.ContainsKey(PolarThemeTokens.AnimationEasing));
    }

    [Fact]
    public void Component_scoped_tokens_carry_a_ComponentScope_value()
    {
        // The 3 WC-instance-scoped tokens (image-aspect-ratio, mini-cart-position,
        // toast-position / toast-duration) should name their scope.
        Assert.NotNull(PolarThemeTokens.AllTokens[PolarThemeTokens.ProductCardImageAspectRatio].ComponentScope);
        Assert.NotNull(PolarThemeTokens.AllTokens[PolarThemeTokens.MiniCartPosition].ComponentScope);
        Assert.NotNull(PolarThemeTokens.AllTokens[PolarThemeTokens.ToastPosition].ComponentScope);
        Assert.NotNull(PolarThemeTokens.AllTokens[PolarThemeTokens.ToastDuration].ComponentScope);
    }
}
