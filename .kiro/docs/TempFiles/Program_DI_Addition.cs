// ============================================================
// ADD this line to Program.cs in the "Revenue Control" DI section
// Place it AFTER: builder.Services.AddScoped<IPaymentService, PaymentService>();
// ============================================================

builder.Services.AddScoped<IPaymentAllocationEngine>(sp =>
    new PaymentAllocationEngine(
        sp.GetRequiredService<PaymentRepository>(),
        sp.GetRequiredService<IFinancialStatusEngine>(),
        sp.GetRequiredService<IPaymentScheduleService>(),
        sp.GetRequiredService<PortalDbContext>()));
