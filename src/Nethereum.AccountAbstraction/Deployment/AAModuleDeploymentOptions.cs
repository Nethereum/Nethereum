namespace Nethereum.AccountAbstraction.Deployment
{
    public class AAModuleDeploymentOptions
    {
        public bool EcdsaValidator { get; set; } = true;
        public bool SmartSession { get; set; } = true;
        public bool SocialRecovery { get; set; } = true;
        public bool OwnableExecutor { get; set; } = false;
    }
}
