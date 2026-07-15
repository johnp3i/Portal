# Design: System Briefing (SuperAdmin)

## Overview

Reuses the v2 briefing card pattern. Queries Portal.Logging DB for errors, slow queries, job status, and failed logins. Queries Portal DB for business metrics. File system for storage.

## Architecture

SystemLogsController -> SystemBriefingService.GenerateBriefingAsync() -> BriefingViewModel -> _DashboardBriefing.cshtml

## Signals

| Signal | Priority | Source | Severity Logic |
|--------|----------|--------|----------------|
| Error trend | 1 | Logging DB | Urgent if 2x spike, Action if > 0, Positive if zero |
| Background jobs | 2 | Logging DB | Urgent if failed, Positive if success |
| Slow queries | 3 | Logging DB | Action if > 10, Positive otherwise |
| Failed logins | 4 | Logging DB | Action if > 5 |
| Business metrics | 5 | Portal DB | Always Positive |
| Storage | 6 | File system | Action if > 10GB |

## Mockup Reference

Locked: `.kiro/docs/mockups/system-briefing-superadmin.html`
