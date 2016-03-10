using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Example.WebApp.Startup))]
namespace Example.WebApp
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
