# Squad Workflows

## Status: Disabled

The squad workflows are currently **disabled**. They were part of an AI-powered team coordination system using GitHub Issues and labels.

## Workflows

| Workflow | File | Purpose |
|----------|------|---------|
| **Squad Triage** | `.github/workflows/squad-triage.yml` | Auto-triages issues labeled `squad` by routing them to the best team member based on keyword matching against roles defined in `.ai-team/team.md` |
| **Squad Issue Assign** | `.github/workflows/squad-issue-assign.yml` | Posts assignment comments and optionally assigns `@copilot` (coding agent) when a `squad:{member}` label is added to an issue |
| **Squad Heartbeat (Ralph)** | `.github/workflows/squad-heartbeat.yml` | Runs every 30 minutes (and on issue/PR events) to find untriaged or unstarted squad issues and auto-route them |
| **Sync Squad Labels** | `.github/workflows/sync-squad-labels.yml` | Creates/updates `squad:*` GitHub labels whenever `.ai-team/team.md` is modified |

## Prerequisites

These workflows require:

1. **`.ai-team/team.md`** — A roster file defining team members, roles, and capabilities
2. **`.ai-team/routing.md`** (optional) — Custom routing rules for issue triage
3. **`COPILOT_ASSIGN_TOKEN`** secret — A GitHub PAT with `issues: write` scope, needed for assigning `@copilot` (coding agent) to issues
4. **Squad labels** — Created automatically by `sync-squad-labels.yml` (e.g., `squad`, `squad:copilot`, `squad:{member}`)

## How to Re-Enable

Each workflow is disabled via an `if: false` condition on the job. To re-enable:

### 1. Squad Triage

In `.github/workflows/squad-triage.yml`, change:

```yaml
jobs:
  triage:
    if: false # Disabled — see docs/squad-workflows.md to re-enable
    # Original condition: github.event.label.name == 'squad'
```

To:

```yaml
jobs:
  triage:
    if: github.event.label.name == 'squad'
```

### 2. Squad Issue Assign

In `.github/workflows/squad-issue-assign.yml`, change:

```yaml
jobs:
  assign-work:
    if: false # Disabled — see docs/squad-workflows.md to re-enable
    # Original condition: startsWith(github.event.label.name, 'squad:')
```

To:

```yaml
jobs:
  assign-work:
    if: startsWith(github.event.label.name, 'squad:')
```

### 3. Squad Heartbeat (Ralph)

In `.github/workflows/squad-heartbeat.yml`, change:

```yaml
jobs:
  heartbeat:
    if: false # Disabled — see docs/squad-workflows.md to re-enable
```

To:

```yaml
jobs:
  heartbeat:
```

(No `if` condition needed — the heartbeat runs on schedule and events.)

### 4. Sync Squad Labels

In `.github/workflows/sync-squad-labels.yml`, change:

```yaml
jobs:
  sync-labels:
    if: false # Disabled — see docs/squad-workflows.md to re-enable
```

To:

```yaml
jobs:
  sync-labels:
```

### After Re-Enabling

1. Create the `.ai-team/team.md` roster file (see workflow source for expected format)
2. Add the `COPILOT_ASSIGN_TOKEN` secret in repo **Settings** → **Secrets** → **Actions** (only needed if using `@copilot` assignment)
3. Run **Sync Squad Labels** manually (Actions → Sync Squad Labels → Run workflow) to create the required labels
4. Add the `squad` label to an issue to test the triage flow
