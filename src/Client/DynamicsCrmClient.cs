using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EfCore.Dynamics365.Client;

/// <summary>
/// Thin wrapper around <see cref="IOrganizationServiceAsync2"/> that handles
/// paged retrieval and provides the four CRUD primitives used by the provider.
/// </summary>
public sealed class DynamicsCrmClient
{
    private readonly IOrganizationServiceAsync2 _service;

    public DynamicsCrmClient(IOrganizationServiceAsync2 service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Executes the query and returns all matching records, walking pages
    /// automatically when <see cref="QueryExpression.TopCount"/> is not set.
    /// </summary>
    public async Task<IList<Entity>> QueryAsync(
        string logicalName,
        QueryExpression query,
        CancellationToken cancellationToken = default
    )
    {
        query.EntityName = logicalName;
        var results = new List<Entity>();

        if (query.TopCount.HasValue)
        {
            // Single bounded fetch — no paging needed.
            var single = await _service
                .RetrieveMultipleAsync(query, cancellationToken)
                .ConfigureAwait(false);
            results.AddRange(single.Entities);
            return results;
        }

        // Walk pages until exhausted.
        var pageNumber = 1;
        string? pagingCookie = null;

        while (true)
        {
            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = pageNumber,
                PagingCookie = pagingCookie,
            };

            var page = await _service
                .RetrieveMultipleAsync(query, cancellationToken)
                .ConfigureAwait(false);
            results.AddRange(page.Entities);

            if (!page.MoreRecords) break;

            pageNumber++;
            pagingCookie = page.PagingCookie;
        }

        return results;
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
}