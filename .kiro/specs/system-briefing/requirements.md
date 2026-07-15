# Requirements: System Briefing (SuperAdmin)

## Introduction

The System Briefing provides SuperAdmin users with a narrative summary of platform health — error trends, background job status, performance metrics, security signals, and business growth indicators. It reuses the same v2 briefing card design from the Dashboard Briefing feature but queries the Logging database and platform-wide metrics.

The briefing is shown exclusively to SuperAdmin users on the System Logs page or Admin Dashboard.

## Requirements

### Requirement 1: Briefing Card Display

1. THE System Logs page SHALL display a System Briefing card at the top, above the log table.
2. THE card SHALL use the same v2 design as the Dashboard Briefing.
3. THE card title SHALL be "System Status".
4. THE card subtitle SHALL adapt to state: "All systems operational", "Elevated activity", or "Issues detected in the last 24 hours".
5. THE card SHALL only be rendered for SuperAdmin users.

### Requirement 2: Error Count Signal (Priority 1)

1. Query Logging database for Error/Fatal in last 24h.
2. Show count, percentage change vs previous 24h, most frequent exception type and source.
3. Urgent if 2x spike, Action if > 0, Positive if zero.
4. Link to filtered error logs.

### Requirement 3: Background Job Status (Priority 2)

1. Check most recent background job execution (success vs failure).
2. Urgent if failed, Positive if succeeded.

### Requirement 4: Slow Query Signal (Priority 3)

1. Count queries exceeding 1 second in last 24h.
2. Action if > 10, informational otherwise.

### Requirement 5: Failed Login Attempts (Priority 4)

1. Count failed auth events in last 24h.
2. Action if > 5.

### Requirement 6: Business Metrics (Priority 5)

1. Active business count, new user registrations.
2. Always Positive.

### Requirement 7: Storage Usage (Priority 6)

1. Total upload storage size.
2. Positive unless > 10 GB.

### Requirement 8: State Determination

1. Any Urgent signal = Critical state (red).
2. Any Action signal (no Urgent) = Warning state (amber).
3. All Positive = Healthy state (green).
