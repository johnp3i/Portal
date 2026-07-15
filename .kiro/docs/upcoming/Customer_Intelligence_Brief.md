# Customer Intelligence — Feature Brief

**Date:** 15 July 2026  
**Status:** Concept — Planning  
**Module:** Insights / Customer Analytics  
**Tier Placement:** Professional (basic) + Enterprise (full)

---

## Vision

Customer Intelligence transforms the Portal from a record-keeping tool into an operational intelligence platform. By analysing invoice, payment, and (optionally) POS transaction data that already exists in the system, businesses gain actionable awareness about their customer base — without any additional data entry.

**Key insight:** Every invoice issued through Portal already has a CustomerId, line items, amounts, dates, and payment history. Customer analytics can launch with zero adoption effort — it derives intelligence from data the platform already generates.

---

## Data Sources

| Source | Customer Linked | Already Exists | Adoption Effort |
|--------|----------------|----------------|-----------------|
| Portal-issued Invoices | ✅ CustomerId on every invoice | ✅ Built | Zero — automatic |
| Portal Payments | ✅ Linked through invoice → customer | ✅ Built | Zero |
| External Sales Import (POS) | ✅ Optional CustomerId | Upcoming | Medium — requires import |
| Revenue Summaries (Z-Reports) | ❌ Aggregate only | Upcoming | N/A for customer analytics |

**Primary source:** Portal invoices + payments. This is enough for meaningful analytics from day one.
**Secondary source:** POS imports enrich the picture for businesses with external sales channels.

---

## Tier Placement Logic

The guiding question: "What does a business at each tier actually need?"

### Foundation Business (solo, 1-2 people)
- Knows their customers personally
- Doesn't need analytics to tell them who their best customer is
- Needs tools to *do* the work, not *analyse* it
- Gets: basic customer stats on the customer list (total invoiced, last invoice date)

### Professional Business (established, 2-5 people, regular volume)
- Enough volume that they can't remember everything
- Benefits from *actionable awareness* — "these 3 customers haven't ordered in a while"
- Operational intelligence that drives follow-up actions
- Gets: Top Customers, At-Risk detection, Revenue per Customer

### Enterprise Business (team, growing, 5+ people)
- Scale where patterns and trends matter (100+ customers)
- Multiple people making decisions from data
- Needs strategic dashboards for meetings and planning
- Gets: Full segmentation, retention curves, lifetime value, acquisition trends

---

## Feature Distribution

| Feature | Foundation | Professional | Enterprise |
|---------|-----------|-------------|-----------|
| Customer list with basic stats (total invoiced, last invoice) | ✅ | ✅ | ✅ |
| Top Customers by Revenue (sorted list, configurable period) | ❌ | ✅ | ✅ |
| Customers At Risk (overdue return pattern detection) | ❌ | ✅ | ✅ |
| Revenue per Customer trend (monthly) | ❌ | ✅ | ✅ |
| New vs Returning Customers (period comparison) | ❌ | ✅ | ✅ |
| Full RFM Segmentation (Recency/Frequency/Monetary scoring) | ❌ | ❌ | ✅ |
| Retention & Churn Dashboards | ❌ | ❌ | ✅ |
| Customer Acquisition Trends | ❌ | ❌ | ✅ |
| Lifetime Value Calculations | ❌ | ❌ | ✅ |
| Customer Segment Comparison (Champions/Loyal/At-Risk/Lost) | ❌ | ❌ | ✅ |
| POS Customer Behaviour (from external imports) | ❌ | ❌ | ✅ |

---

## Professional Tier — Actionable Awareness

### Top Customers

A sorted list showing customers ranked by total revenue for a selected period (month/quarter/year).

**Columns:** Customer Name, Invoice Count, Total Revenue, Average Invoice Value, Last Invoice Date

**Use case:** "Who are my most valuable customers right now?"

### Customers At Risk

Detection based on each customer's own historical pattern. If a customer's average gap between invoices is 30 days, and they haven't had an invoice in 60 days — flag them.

**Logic:** `daysSinceLastInvoice > (averageGapDays * 2)` = At Risk

**Display:** List of at-risk customers with: Name, Last Invoice Date, Normal Frequency, Days Overdue

**Use case:** "These 3 customers usually order monthly but haven't in 2 months — follow up."

### Revenue per Customer Trend

A simple line/bar chart showing monthly revenue per customer (or top N customers).

**Use case:** "Is this customer's spend growing, stable, or declining?"

### New vs Returning

Period comparison: how many customers are new (first invoice this period) vs returning (had invoices in prior periods).

**Use case:** "Am I growing my customer base or just serving the same people?"

---

## Enterprise Tier — Strategic Intelligence

### RFM Segmentation

Score every customer on three dimensions (1-5 each):
- **Recency** — Days since last invoice (lower = better)
- **Frequency** — Invoices per month (higher = better)
- **Monetary** — Average invoice value (higher = better)

Combine into segments:

| Segment | R | F | M | Action |
|---------|---|---|---|--------|
| Champions | 5 | 5 | 5 | Reward, upsell |
| Loyal Customers | 4 | 4 | 4 | Maintain relationship |
| Potential Loyalists | 5 | 3 | 3 | Nurture frequency |
| At Risk | 2 | 3 | 3 | Re-engage immediately |
| Can't Lose Them | 1 | 4 | 5 | Urgent win-back |
| Hibernating | 1 | 1 | 1 | Consider inactive |
| New Customers | 5 | 1 | 1 | Onboard, build habit |

**Dashboard:** Pie chart of customer segments, trends over time, drill-down per segment.

### Retention & Churn

- **Retention Rate:** % of customers active in month M who were also active in month M-1
- **Churn Rate:** 1 - Retention Rate
- **Cohort Analysis:** Customers acquired in January — what % are still active in Feb, Mar, Apr...
- **Retention Curve:** Visual showing how quickly customers drop off after first purchase

### Customer Lifetime Value (CLV)

Simple calculation: `Average Invoice Value × Average Invoices Per Year × Average Customer Lifespan (years)`

Display per customer and as business-wide averages.

### Acquisition Trends

- New customers per month (line chart)
- Source of customers (if Sales module is active: which lead source produced the most customers?)
- Time from first lead to first invoice (conversion velocity)

---

## Technical Approach

### No New Tables (Phase 1)

Customer Intelligence is a **read-only analytics layer**. It queries existing data:
- `[dbo].[Invoice]` — CustomerId, InvoiceDate, TotalAmount, InvoiceStatusTypeId
- `[dbo].[Payment]` — Amount, PaymentDate (via Invoice)
- `[dbo].[Customer]` — Id, Name, CreatedAtUtc
- `[dbo].[ExternalSalesRecord]` — CustomerId, TransactionDate, TotalAmount (when available)

No new schema needed. Pure computation from existing tables.

### Computation Strategy

**Option A: Real-time queries** — Calculate metrics on page load. Simple, always accurate, but may be slow for businesses with 10,000+ invoices.

**Option B: Materialised snapshots** — Nightly background job computes metrics and stores in a `[dbo].[CustomerMetricsSnapshot]` table. Fast reads, slightly stale (24h max).

**Recommendation:** Start with real-time (Option A) for Professional tier features (simple queries, limited data). Move to snapshots (Option B) for Enterprise features (segmentation, cohort analysis) that involve heavier computation.

### Service Layer

```csharp
public interface ICustomerIntelligenceService
{
    // Professional tier
    Task<List<TopCustomerItem>> GetTopCustomersAsync(DateOnly from, DateOnly to, int limit = 10);
    Task<List<AtRiskCustomerItem>> GetAtRiskCustomersAsync();
    Task<List<CustomerRevenueTrendItem>> GetCustomerRevenueTrendAsync(int customerId, int months = 12);
    Task<NewVsReturningResult> GetNewVsReturningAsync(DateOnly from, DateOnly to);

    // Enterprise tier
    Task<List<RfmSegmentResult>> GetRfmSegmentationAsync();
    Task<RetentionResult> GetRetentionDataAsync(int months = 12);
    Task<decimal> GetCustomerLifetimeValueAsync(int customerId);
    Task<List<AcquisitionTrendItem>> GetAcquisitionTrendsAsync(int months = 12);
}
```

---

## Phasing

### Phase 1: Professional Features
- Top Customers list (sorted by revenue, configurable period)
- At-Risk Customers detection (pattern-based)
- Revenue per Customer trend chart
- New vs Returning comparison
- All computed from existing invoice data — no new tables

### Phase 2: Enterprise Features
- RFM Segmentation with visual dashboard
- Retention & Churn dashboards
- Customer Lifetime Value
- Acquisition Trends
- CustomerMetricsSnapshot table for performance
- Nightly computation job

### Phase 3: POS Integration
- Include ExternalSalesRecord data in all calculations
- Cross-channel customer view (portal + POS combined)
- Enhanced frequency/recency detection using all transaction sources

---

## Market Positioning

This feature positions Portal as an **Operational Intelligence** platform rather than just a billing tool:

- **Foundation:** "Run your business" (tools)
- **Professional:** "Understand your business" (awareness + automation)
- **Enterprise:** "Grow your business" (strategic intelligence)

Customer Intelligence is the bridge between "I issue invoices" and "I understand my customers." No additional data entry required — the intelligence emerges from operations the user is already doing.

---

## Open Questions

1. **Dashboard location:** Separate "Insights" top-level nav item? Or a sub-section under "Customers"?
2. **Alerting:** Should At-Risk detection trigger notifications? ("Customer X hasn't ordered in 45 days — their average is 14 days")
3. **Export:** Should analytics data be exportable to CSV/PDF for presentations?
4. **Sales Pipeline integration:** When a Lead converts to Customer, should their CLV tracking start from lead creation date?

---

## Related Documents

- [Revenue Ingestion Brief](./Revenue_Ingestion_Brief.md) — POS data source for enhanced analytics
- [Sales Module Brief](./Sales_Module_Brief.md) — Lead-to-Customer lifecycle
- [Subscription Tier Model](../Subscription_Tier_Model.md) — Tier placement
- [Portal Product Overview](../Portal_Product_Overview_2026-07-10.md) — Platform features
