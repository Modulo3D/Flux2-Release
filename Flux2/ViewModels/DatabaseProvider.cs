using DynamicData.Kernel;
using LiteDB;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO;
using System.ServiceModel;

namespace Flux.ViewModels
{
    public class DatabaseProvider : ReactiveObject, IFluxDatabaseProvider
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
        }

        public bool Initialize(Action<ILocalDatabase> initialize_callback = default)
        {
            try
            {
                if (Database.HasValue)
                    Database.Value.Dispose();

                var delorean_uri = "http://deloreanservice.azurewebsites.net/delorean.svc/delorean";
                using var client = new BasicHttpClient<IDelorean>(delorean_uri);
                var channel = client.CreateChannel();
                var response = channel.GetProfile(DatabaseVisibility.Public);
                ((IClientChannel)channel).Close();

                if (response.Length > 0)
                {
                    if (Files.Database.Exists)
                        Files.Database.Delete();
                    File.WriteAllBytes(Files.Database.FullName, response);
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
                    database.Initialize(Modulo3DStandard.Database.RegisterCuraSlicerDatabase);

                    initialize_callback?.Invoke(database);
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
