using System;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Peekerino.Configuration;
using Peekerino.Services;
using Peekerino.Services.Summaries;
using Peekerino.Shell;
using Peekerino.UI;

namespace Peekerino
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var services = new ServiceCollection();
            ConfigureServices(services, configuration);

            using var provider = services.BuildServiceProvider();
            var mainForm = provider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(configuration);
            services.AddOptions<PeekerinoOptions>()
                .Bind(configuration.GetSection("Peekerino"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<PeekerinoOptions>>().Value);

            services.AddSingleton<ExplorerSelectionProvider>();
            services.AddSingleton<FileSummaryService>();

            services.AddSingleton<IFileSummarizer, XmlFileSummarizer>();
            services.AddSingleton<IFileSummarizer, CsvFileSummarizer>();
            services.AddSingleton<IFileSummarizer, ExcelFileSummarizer>();
            services.AddSingleton<IFileSummarizer, JsonFileSummarizer>();
            services.AddSingleton<IFileSummarizer, ArchiveFileSummarizer>();
            services.AddSingleton<IFileSummarizer, TextFileSummarizer>();
            services.AddSingleton<IFileSummarizer, BinaryFileSummarizer>();

            services.AddTransient<MainForm>();
        }
    }
}
