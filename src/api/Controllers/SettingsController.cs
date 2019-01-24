using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ticketing.Worker.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private IOptionsSnapshot<AppConfiguration> _appSettings;
        public SettingsController(IOptionsSnapshot<AppConfiguration> appSettings)
        {
            _appSettings = appSettings;
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new List<string>()
            {
                $"ValueFromAppSettings: {_appSettings.Value.ValueFromAppSettings}",
                $"ValueFromKubernetesEnvVariableValue: {_appSettings.Value.ValueFromKubernetesEnvVariable}",
                $"ValueOverride: {_appSettings.Value.ValueOverride}",
                $"ValueFromKubernetesSecret: {_appSettings.Value.ValueFromKubernetesSecret}"
            };
        }
    }
}