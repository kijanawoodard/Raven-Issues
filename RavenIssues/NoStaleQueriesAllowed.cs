using Raven.Client;
using Raven.Client.Listeners;

namespace RavenIssues
{
    public class NoStaleQueriesAllowed : IDocumentQueryListener
    {
        public void BeforeQueryExecuted(IDocumentQueryCustomization queryCustomization)
        {
            queryCustomization.WaitForNonStaleResults();
        }
    }
}