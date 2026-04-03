using EfCore.Dynamics365.Extensions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace EfCore.Dynamics.FunctionalTests.TestUtilities;

internal sealed class DynamicsTestStore : TestStore
{
    private IOrganizationServiceAsync2 OrganizationServiceAsync2 { get; }

    private DynamicsTestStore(string name) : base(name, true)
    {
        var fakedContext = MiddlewareBuilder
            .New()
            // fake transaction/multiple request support
            .Use(next => (context, request) =>
            {
                switch (request)
                {
                    case ExecuteTransactionRequest transactionRequest:
                    {
                        OrganizationResponseCollection responses = [];
                        foreach (var req in transactionRequest.Requests)
                        {
                            var response = next(context, req);
                            if (transactionRequest.ReturnResponses is true) responses.Add(response);
                        }

                        return new ExecuteTransactionResponse
                        {
                            Results = [new KeyValuePair<string, object>("Responses", responses)]
                        };
                    }
                    case ExecuteMultipleRequest multipleRequest:
                    {
                        ExecuteMultipleResponseItemCollection responses = [];
                        foreach (var req in multipleRequest.Requests)
                        {
                            var response = next(context, req);
                            if (multipleRequest.Settings.ReturnResponses) 
                                responses.Add(new ExecuteMultipleResponseItem { Response = response });
                        }

                        return new ExecuteMultipleResponse
                        {
                            Results = [new KeyValuePair<string, object>("Responses", responses)]
                        };
                    }
                    default:
                        return next(context, request);
                }
            })
            .AddCrud()
            .UseCrud()
            .SetLicense(FakeXrmEasyLicense.NonCommercial)
            .Build();

        OrganizationServiceAsync2 = fakedContext.GetAsyncOrganizationService2();
    }

    public static DynamicsTestStore Create(string name) => new(name);

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder) =>
        builder.UseDynamics365(OrganizationServiceAsync2);

    public override void Clean(DbContext context)
    {
        // TODO:
    }
}