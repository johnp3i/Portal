# Implementation Plan: System Briefing (SuperAdmin)

## Overview

Server-rendered system health briefing for SuperAdmin users on the System Logs page. Reuses `_DashboardBriefing.cshtml` partial and `BriefingViewModel`. Queries LoggingDbContext and PortalDbContext.

## Tasks

- [x] 1. Service
  - [x] 1.1 Create ISystemBriefingService interface
  - [x] 1.2 Create SystemBriefingService with 6 signal evaluators
  - [x] 1.3 Error trend signal (Priority 1)
  - [x] 1.4 Background job status signal (Priority 2)
  - [x] 1.5 Slow queries signal (Priority 3)
  - [x] 1.6 Failed logins signal (Priority 4)
  - [x] 1.7 Business metrics signal (Priority 5)
  - [x] 1.8 Storage usage signal (Priority 6)

- [x] 2. Integration
  - [x] 2.1 Register in DI
  - [x] 2.2 Wire into System Logs controller (SuperAdmin only)
  - [x] 2.3 Embed _DashboardBriefing partial on System Logs view

- [x] 3. Final checkpoint

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "1.6", "1.7", "1.8"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3"] }
  ]
}
```
