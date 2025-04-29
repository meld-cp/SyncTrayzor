namespace SyncTrayzor.Services.UpdateManagement
{
    public interface IUpdateCheckerFactory
    {
        IUpdateChecker CreateUpdateChecker(string baseUrl, string variant);
    }

    public class UpdateCheckerFactory : IUpdateCheckerFactory
    {
        private readonly IAssemblyProvider assemblyProvider;
        private readonly IUpdateNotificationClientFactory updateNotificationClientFactory;

        public UpdateCheckerFactory(IAssemblyProvider assemblyProvider, IUpdateNotificationClientFactory updateNotificationClientFactory)
        {
            this.assemblyProvider = assemblyProvider;
            this.updateNotificationClientFactory = updateNotificationClientFactory;
        }

        public IUpdateChecker CreateUpdateChecker(string baseUrl, string variant)
        {
            return new UpdateChecker(assemblyProvider.Version, assemblyProvider.ProcessorArchitecture, variant, updateNotificationClientFactory.CreateUpdateNotificationClient(baseUrl));
        }
    }
}
