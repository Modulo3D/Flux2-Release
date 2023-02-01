using DynamicData.Kernel;
using LiteDB;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.IO;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class DatabaseProvider : ReactiveObjectRC<DatabaseProvider>, IFluxDatabaseProvider
    {
        public FluxViewModel Flux { get; }

        private Optional<ILocalDatabase> _Database;
        public Optional<ILocalDatabase> Database
        {
            get => _Database;
            private set => this.RaiseAndSetIfChanged(ref _Database, value);
        }

        public DatabaseProvider(FluxViewModel main)
        {
            Flux = main;
            this.WhenAnyValue(v => v.Database)
                .DisposePrevious()
                .SubscribeRC(this);
        }

        public override void Dispose()
        {
            base.Dispose();
            Database.IfHasValue(d => d.Dispose());
        }

        public async Task<bool> InitializeAsync(Func<ILocalDatabase, Task> initialize_callback = default)
        {
            try
            {
                if (Database.HasValue)
                    Database.Value.Dispose();
                
                //var service = new DeLoreanClient("http://localhost:5004/DeLorean/");
                var service = new DeLoreanClient("http://deloreanservice.azurewebsites.net/delorean");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await service.GetProfileAsync(cts.Token);

                if (response.Length > 0)
                {
                    if (Files.Database.Exists)
                        Files.Database.Delete();

                    var bytes = Convert.FromBase64String(response);
                    bytes = await bytes.UnzipAsync();

                    File.WriteAllBytes(Files.Database.FullName, bytes);
                }
            }
            catch (Exception ex)
            {
                Flux.Messages?.LogMessage(DatabaseResponse.DATABASE_DOWNLOAD_EXCEPTION, ex: ex);
            }

            ILocalDatabase database;
            try
            {
                if (Files.Database.Exists)
                {
                    var connection = new ConnectionString()
                    {
                        Filename = Files.Database.FullName,
                    };

                    database = new LocalDatabase(connection);
                    Modulo3DNet.Database.RegisterCuraSlicerDatabase(database);

                    await initialize_callback?.Invoke(database);
                    Database = database.ToOptional();

                    return true;
                }
                else
                {
                    Flux.Messages?.LogMessage(DatabaseResponse.DATABASE_NOT_FOUND);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Flux.Messages?.LogMessage(DatabaseResponse.DATABASE_LOAD_EXCEPTION, ex: ex);
                return false;
            }
        }
    }
}
