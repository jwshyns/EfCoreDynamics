using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace EfCore.Dynamics365.Client;

internal interface IDynamicsClient
{
    public List<Entity> Query(QueryExpression query);
    
    /// <summary>
    /// Executes the query and returns all matching records, walking pages
    /// automatically when <see cref="QueryExpression.TopCount"/> is not set.
    /// </summary>
    public IAsyncEnumerable<DataCollection<Entity>> QueryAsync(
        QueryExpression query,
        CancellationToken cancellationToken = default
    );

    /// <summary>Creates a record and returns its new primary-key GUID.</summary>
    public Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>Updates a record (PATCH semantics — only supplied attributes are changed).</summary>
    public Task UpdateAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>Deletes a record by logical name and primary-key GUID.</summary>
    public Task DeleteAsync(string logicalName, Guid id, CancellationToken cancellationToken = default);

    public Task<ExecuteMultipleResponse> ExecuteMultipleAsync(
        OrganizationRequestCollection requests,
        CancellationToken cancellationToken = default
    );

    public Task<ExecuteTransactionResponse> ExecuteTransactionAsync(
        OrganizationRequestCollection requests,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Thin wrapper around <see cref="IOrganizationServiceAsync2"/> that handles
/// paged retrieval and provides the four CRUD primitives used by the provider.
/// </summary>
internal sealed class DynamicsClient : IDynamicsClient
{
    private readonly IOrganizationServiceAsync2 _service;

    public DynamicsClient(IDbContextOptions dbContextOptions)
    {
        _service = dbContextOptions.FindExtension<DynamicsOptionsExtension>().OrganisationServiceAsync2!;
    }
    
    public List<Entity> Query(QueryExpression query)
    {
        // Walk pages until exhausted.
        var pageNumber = 1;
        string? pagingCookie = null;
        List<Entity> entities = [];

        while (true)
        {
            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = pageNumber,
                PagingCookie = pagingCookie
            };

            var page = _service.RetrieveMultiple(query);
            entities.AddRange(page.Entities);

            if (!page.MoreRecords) break;

            pageNumber++;
            pagingCookie = page.PagingCookie;
        }

        return entities;
    }

    /// <summary>
    /// Executes the query and returns all matching records, walking pages
    /// automatically when <see cref="QueryExpression.TopCount"/> is not set.
    /// </summary>
    public async IAsyncEnumerable<DataCollection<Entity>> QueryAsync(
        QueryExpression query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // Walk pages until exhausted.
        var pageNumber = 1;
        string? pagingCookie = null;

        while (true)
        {
            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = pageNumber,
                PagingCookie = pagingCookie
            };

            var page = await _service
                .RetrieveMultipleAsync(query, cancellationToken)
                .ConfigureAwait(false);

            yield return page.Entities;

            if (!page.MoreRecords) break;

            pageNumber++;
            pagingCookie = page.PagingCookie;
        }
    }

    /// <summary>Creates a record and returns its new primary-key GUID.</summary>
    public Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken = default)
        => _service.CreateAsync(entity, cancellationToken);

    /// <summary>Updates a record (PATCH semantics — only supplied attributes are changed).</summary>
    public Task UpdateAsync(Entity entity, CancellationToken cancellationToken = default)
        => _service.UpdateAsync(entity, cancellationToken);

    /// <summary>Deletes a record by logical name and primary-key GUID.</summary>
    public Task DeleteAsync(string logicalName, Guid id, CancellationToken cancellationToken = default)
        => _service.DeleteAsync(logicalName, id, cancellationToken);

    public async Task<ExecuteMultipleResponse> ExecuteMultipleAsync(
        OrganizationRequestCollection requests,
        CancellationToken cancellationToken = default
    ) =>
        (ExecuteMultipleResponse)await _service.ExecuteAsync(
            new ExecuteMultipleRequest
                { Requests = requests, Settings = new ExecuteMultipleSettings { ReturnResponses = true } },
            cancellationToken);


    public async Task<ExecuteTransactionResponse> ExecuteTransactionAsync(
        OrganizationRequestCollection requests,
        CancellationToken cancellationToken = default
    ) =>
        (ExecuteTransactionResponse)await _service.ExecuteAsync(
            new ExecuteTransactionRequest { Requests = requests, ReturnResponses = true },
            cancellationToken);
}