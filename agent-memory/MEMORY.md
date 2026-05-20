# Memory Index

- [Project State](project_state.md) — PolarSharp build progress, phases complete, what's left
- [Key API Patterns](api_patterns.md) — Kiota EmptyPathSegment, PolarClient resource access, Finbuckle method names
- [No agentic-master in this project](feedback_no_agentic_master.md) — use raw `git` directly; do NOT invoke agentic-master for any operation in this repo (supersedes the two notes below for THIS project)
- [Commit Message Provider](feedback_commit_message_provider.md) — historical context; Anthropic-drafted messages still required, now via raw `git commit -m`
- [agentic-master --no-confirm gotcha](feedback_agentic_master_no_confirm.md) — historical context only; agentic-master is no longer used in this project
- [Never echo secrets](feedback_never_echo_secrets.md) — `${VAR:-fallback}` still expands the value when VAR is set; branch at shell-statement level (`test/[[/if`) not inside `echo` arguments
- [Count packages via csproj find](feedback_count_packages_via_csproj.md) — repo ships 31 packages (30 in src/ + Templates in templates/); never claim count from `ls src/` alone
- [Rotate secrets via pbpaste](feedback_secret_rotation_via_pbpaste.md) — default pattern: `! pbpaste | gh secret set NAME --repo ...` so secret never enters my conversation context; then `pbcopy < /dev/null` to clear clipboard
- [Live Polar tests required](feedback_live_polar_tests_required.md) — every IPolarXxxApi wrapper ships with a paired test exercising user-code → our wrapper → live sandbox (not raw HttpClient bypassing the wrapper); pattern set by V20-002/003/004
- ["Unhandled node type" always proceed](feedback_unhandled_node_type_always_proceed.md) — standing Yes on the "Unhandled node type: string / Do you want to proceed?" prompt; never block or re-ask
- [Agent-driven marketplace construction](project_agent_driven_marketplaces.md) — PolarSharp tenant websites built by AI agents that decision-tree-select WCs; documentation must be machine-parseable; catalog can be deliberately large
- [Verify .NET 10 capabilities before recommending build-vs-use](feedback_verify_dotnet_capabilities.md) — don't claim .NET features are missing from training cutoff; WebSearch first (e.g. passkey support IS native in ASP.NET Core 10)
- [Every new entity must honor 5-layer tenant isolation](feedback_tenant_isolation_every_new_entity.md) — ITenantOwned + RLS + SQLite-per-file + MariaDB filter + Cosmos partition + single-tenant mode; explicit acceptance criteria, not assumed
- [Per-tenant SSO architecture (BYOK + per-provider packages)](project_tenant_sso_per_provider.md) — Google/Microsoft/Facebook/Apple/GitHub/etc.; per-provider packages like translation pattern; BYOK + real-time validation; new polar-sso-button-group WC
