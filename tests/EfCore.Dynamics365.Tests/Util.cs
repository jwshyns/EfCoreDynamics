using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;

namespace EfCore.Dynamics365.Tests;

public static class Util
{
    public static IXrmFakedContext BuildContext()
    {
        return MiddlewareBuilder
            .New()
            .AddCrud()
            .UseCrud()
            .SetLicense(FakeXrmEasyLicense.NonCommercial)
            .Build();
    }
}