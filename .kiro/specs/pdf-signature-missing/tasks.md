# Tasks — PDF Signature Missing Bugfix

## Task 1: Fix margin-top:auto in Invoice Snapshot.cshtml

- [ ] 1.1 Open `Portal.Web/Views/Invoice/Snapshot.cshtml` and locate the document footer wrapper div (~line 399)
- [ ] 1.2 Change `margin-top:auto` to `margin-top:40px` in the inline style
- [ ] 1.3 Verify the full style attribute reads: `break-inside:avoid;page-break-inside:avoid;margin-top:40px;padding-top:32px;`

## Task 2: Build Verification

- [ ] 2.1 Run `dotnet build` on the Portal solution to confirm no syntax or compilation errors
