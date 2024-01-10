namespace Logship.WmataPuller.Config
{
    public class AmtrackConfiguration
    {
        public bool Enabled { get; set; } = true;

        public string AmtrackEndpoint { get; set; } = string.Empty;

        public bool IsValid(out string whyNot)
        {
            if (false == this.Enabled)
            {
                whyNot = string.Empty;
                return true;
            }

            if (string.IsNullOrEmpty(this.AmtrackEndpoint))
            {
                whyNot = "AmtrackEndpoint must be set. e.g. https://amtrak.p.mashape.com";
                return false;
            }

            whyNot = null!;
            return true;
        }
    }
}
