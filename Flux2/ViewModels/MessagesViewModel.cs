using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using OSAI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;

namespace Flux.ViewModels
{
    public class FluxMessage : RemoteControl<FluxMessage>, IFluxMessage
    {
        [RemoteOutput(false, typeof(DateTimeConverter<DateTimeFormat>))]
        public DateTime TimeStamp { get; set; }

        [RemoteOutput(false)]
        public MessageLevel Level { get; set; }
        [RemoteOutput(false)]
        public string Title { get; set; }
        [RemoteOutput(false)]
        public string Text { get; set; }
        [RemoteOutput(false)]
        public int ErrCode { get; set; }

        public FluxMessage(string title, string text, MessageLevel level, DateTime timestamp, int errCode) : base($"message??{errCode}:{timestamp.Serialize()}")
        {
            TimeStamp = timestamp;
            ErrCode = errCode;
            Level = level;
            Title = title;
            Text = text;
        }
        public FluxMessage(int errCode, SYSTEMTIMECNDEX systime, MSGTEXTarray message_text_array, MessageLevel level) : base($"message??{errCode}:{ParseTimeStamp(systime)}")
        {
            Level = level;
            ErrCode = errCode;
            TimeStamp = ParseTimeStamp(systime);

            Title = level switch
            {
                MessageLevel.WARNING => "ANOMALIA PLC",
                MessageLevel.DEBUG => "DEBUG PLC",
                MessageLevel.EMERG => "EMERGENZA PLC",
                MessageLevel.ERROR => "ERRORE PLC",
                MessageLevel.INFO => "INFO PLC",
                _ => ""
            };

            var message = message_text_array.FirstOrDefault();
            if (message == default)
                Text = "";

            var message_builder = new StringBuilder();
            if (!string.IsNullOrEmpty(message.line1))
                message_builder.AppendLine(message.line1);
            if (!string.IsNullOrEmpty(message.line2))
                message_builder.AppendLine(message.line2);
            if (!string.IsNullOrEmpty(message.line3))
                message_builder.AppendLine(message.line3);
            if (!string.IsNullOrEmpty(message.line4))
                message_builder.AppendLine(message.line4);

            var message_str = message_builder.ToString();
            Text = message_str.TrimEnd(Environment.NewLine.ToCharArray());
        }
        static DateTime ParseTimeStamp(SYSTEMTIMECNDEX systime)
        {
            return DateTime.Parse($"{systime.wDay}/{systime.wMonth}/{systime.wYear} {systime.wHour}:{systime.wMinute}:{systime.wSecond}");
        }
        /*public static bool TryParse(Optional<ReadCurrentErrorMsgExResponse> error_response, out FluxMessage out_error)
        {
            out_error = default;
            try
            {
                if (!error_response.HasValue)
                    return false;

                if (!OSAI_Connection.ProcessResponse(
                    error_response.Value.Body.retval,
                    error_response.Value.Body.ErrClass,
                    error_response.Value.Body.ErrNum))
                    return false;

                var error_body = error_response.Value.Body;
                var error_data = error_body.Data;
                if (error_data.CodeErr == 0)
                    return false;

                var message_text = error_body.Text.FirstOrDefault();
                if (message_text == default)
                    return false;

                var message_level = message_text.line1.StartsWith("Warning") ? MessageLevel.WARNING : MessageLevel.ERROR;

                var err_code = (int)(error_body.ErrClass * 1000 + error_body.ErrNum);
                out_error = new FluxMessage(err_code, error_data.SystemTime, error_body.Text, message_level);
                return !string.IsNullOrEmpty(out_error.Text);
            }
            catch
            {
                return false;
            }
        }
        public static bool TryParse(Optional<ReadCurrentAnomalyMsgExResponse> anomaly_response, out FluxMessage out_anomaly)
        {
            out_anomaly = default;
            try
            {
                if (!anomaly_response.HasValue)
                    return false;

                if (!OSAI_Connection.ProcessResponse(
                    anomaly_response.Value.Body.retval,
                    anomaly_response.Value.Body.ErrClass,
                    anomaly_response.Value.Body.ErrNum))
                    return false;

                var anomaly_body = anomaly_response.Value.Body;
                var anomaly_data = anomaly_body.Data;
                if (anomaly_data.CodeErr == 0)
                    return false;

                var err_code = (int)(anomaly_body.ErrClass * 1000 + anomaly_body.ErrNum);
                out_anomaly = new FluxMessage(err_code, anomaly_data.SystemTime, anomaly_body.Text, MessageLevel.WARNING);
                return !string.IsNullOrEmpty(out_anomaly.Text);
            }
            catch
            {
                return false;
            }
        }
        public static bool TryParse(Optional<ReadCurrentEmergMsgExResponse> emergency_response, out FluxMessage out_emergency)
        {
            out_emergency = default;
            try
            {
                if (!emergency_response.HasValue)
                    return false;

                if (!OSAI_Connection.ProcessResponse(
                    emergency_response.Value.Body.retval,
                    emergency_response.Value.Body.ErrClass,
                    emergency_response.Value.Body.ErrNum))
                    return false;

                var emergency_body = emergency_response.Value.Body;
                var emergency_data = emergency_body.Data;
                if (emergency_data.CodeErr == 0)
                    return false;


                var err_code = (int)(emergency_body.ErrClass * 1000 + emergency_body.ErrNum);
                out_emergency = new FluxMessage(err_code, emergency_data.SystemTime, emergency_body.Text, MessageLevel.EMERG);
                return !string.IsNullOrEmpty(out_emergency.Text);
            }
            catch
            {
                return false;
            }
        }*/
    }

    public struct MessageCounter
    {
        public int InfoMessagesCount { get; }
        public int DebugMessagesCount { get; }
        public int WarningMessagesCount { get; }
        public int ErrorMessagesCount { get; }
        public int EmergencyMessagesCount { get; }

        public MessageCounter(int debug, int info, int warning, int error, int emerg)
        {
            InfoMessagesCount = info;
            DebugMessagesCount = debug;
            ErrorMessagesCount = error;
            WarningMessagesCount = warning;
            EmergencyMessagesCount = emerg;
        }
    }

    public class MessagesViewModel : FluxRoutableViewModel<MessagesViewModel>, IMessageViewModel
    {
        public ISourceList<IFluxMessage> Messages { get; set; }

        [RemoteContent(true)]
        public IObservableList<IFluxMessage> SortedMessages { get; set; }

        private MessageLevel _LevelFilter = MessageLevel.INFO;
        public MessageLevel LevelFilter
        {
            get => _LevelFilter;
            set => this.RaiseAndSetIfChanged(ref _LevelFilter, value);
        }

        private readonly ObservableAsPropertyHelper<MessageCounter> _MessageCounter;
        public MessageCounter MessageCounter => _MessageCounter.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetMessagesCommand { get; private set; }

        public MessagesViewModel(FluxViewModel flux) : base(flux)
        {
            Messages = new SourceList<IFluxMessage>();
            Messages.Connect()
                .DisposeMany()
                .Subscribe()
                .DisposeWith(Disposables);

            var filter = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsConnecting),
                this.WhenAnyValue(v => v.LevelFilter),
                (connecting, level) =>
                {
                    bool filter(IFluxMessage message) => connecting.HasValue && !connecting.Value && message.Level >= level;
                    return (Func<IFluxMessage, bool>)filter;
                });

            SortedMessages = Messages.Connect()
                .AutoRefresh(m => m.TimeStamp)
                .Filter(filter)
                .Sort(Comparer<IFluxMessage>.Create((tm1, tm2) => tm1.TimeStamp.CompareTo(tm2.TimeStamp)))
                .AsObservableList();

            _MessageCounter = SortedMessages.Connect()
                .QueryWhenChanged(messages =>
                {
                    int infoCount = 0;
                    int debugCount = 0;
                    int emergCount = 0;
                    int errorCount = 0;
                    int warningCount = 0;
                    foreach (var message in messages)
                    {
                        switch (message.Level)
                        {
                            case MessageLevel.DEBUG:
                                debugCount++;
                                break;
                            case MessageLevel.INFO:
                                infoCount++;
                                break;
                            case MessageLevel.WARNING:
                                warningCount++;
                                break;
                            case MessageLevel.ERROR:
                                errorCount++;
                                break;
                            case MessageLevel.EMERG:
                                emergCount++;
                                break;
                        }
                    }
                    return new MessageCounter(debugCount, infoCount, warningCount, errorCount, emergCount);
                }).ToProperty(this, v => v.MessageCounter);

            ResetMessagesCommand = ReactiveCommand.Create(Messages.Clear);
        }

        // LOG MESSAGES

        // 100xx - Errore Reset PLC
        // 200xx - Errore Hold PLC
        // 300xx - Errore Cycle PLC
        // 400xx - Errore Close PLC
        // 500xx - Errore Communication PLC
        // 600xx - Errore Setup PLC
        // 700xx - Errore Connect PLC
        // 800xx - Errore MDI PLC
        // 900xx - Errore Mode PLC
        // 110xx - Errore return profile
        // 120xx - Errore execute paramacro
        // 130xx - Errore prepare part program
        // 140xx - Errore Material change
        // 150xx - Errore PLC process data
        // 160xx - Errore di eccezione
        // 170xx - Errore di lettura / scrittura variabile
        // 180xx - Errore di homing
        // 190xx - Errore NFC
        // 210xx - Errore Database
        // 220xx - Errore Network
        // 230xx - Errore File
        // 240xx - Errore update plc
        // 250xx - Errore salvataggio stato
        // 260xx - Errore salvataggio impostazioni
        // 270xx - Errore cambio utensile
        // 280xx - Errore pressione
        // 290xx - Errore camera calda
        // 300xx - Errore piatto caldo
        // 310xx - Errore chiavistelli
        // 320xx - Errore vuoto
        // 330xx - Errore blocco tag
        // 340xx - Errore sblocco tag
        // 350xx - Errore scrivi tag
        // 360xx - Errore leggi tag
        // 370xx - Errore backup tag
        // 380xx - Errore motori

        public void LogMessage(IFluxMessage message)
        {
            RxApp.MainThreadScheduler.Schedule(() => Messages.Add(message));
            Console.WriteLine(message.Title);
            Console.WriteLine(message.Text);
            Console.WriteLine();    
        }

        public void LogMessage(string title, string text, MessageLevel level, int errCode)
        {
            LogMessage(title, text, level, DateTime.Now, errCode);
        }
        public void LogMessage(string title, string text, MessageLevel level, DateTime timestamp, int errCode)
        {
            LogMessage(new FluxMessage(title, text, level, timestamp, errCode));
        }

        public void LogMessage<TRData, TWData>(IFLUX_Variable<TRData, TWData> plc_var, bool reading)
        {
            var error_code = reading ? 17001 : 17002;
            var title = $"Errore di {(reading ? "lettura" : "scrittura")} variabile";
            LogMessage(title, plc_var.Name, MessageLevel.ERROR, error_code);
        }
        public void LogMessage<TRData, TWData>(Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_plc_var, bool reading)
        {
            var variable = get_plc_var(Flux.ConnectionProvider.VariableStore);
            LogMessage(variable, reading);
        }
        public void LogMessage<TRData, TWData>(IFLUX_Array<TRData, TWData> plc_array, ushort position, bool reading)
        {
            var error_code = reading ? 17001 : 17002;
            var title = $"Errore di {(reading ? "lettura" : "scrittura")} variabile";
            LogMessage(title, $"{plc_array.Name}, pos: {position}", MessageLevel.ERROR, error_code);
        }
        public void LogMessage<TRData, TWData>(Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_plc_array, ushort position, bool reading)
        {
            var array = get_plc_array(Flux.ConnectionProvider.VariableStore);
            LogMessage(array, position, reading);
        }

        public void LogMessage<TRData, TWData>(Optional<IFLUX_Variable<TRData, TWData>> plc_var, bool reading)
        {
            if (plc_var.HasValue)
                LogMessage(plc_var.Value, reading);
        }
        public void LogMessage<TRData, TWData>(Func<IFLUX_VariableStore, Optional<IFLUX_Variable<TRData, TWData>>> get_plc_var, bool reading)
        {
            var variable = get_plc_var(Flux.ConnectionProvider.VariableStore);
            if (variable.HasValue)
                LogMessage(variable.Value, reading);
        }
        public void LogMessage<TRData, TWData>(Optional<IFLUX_Array<TRData, TWData>> plc_array, ushort position, bool reading)
        {
            if (plc_array.HasValue)
                LogMessage(plc_array.Value, position, reading);
        }
        public void LogMessage<TRData, TWData>(Func<IFLUX_VariableStore, Optional<IFLUX_Array<TRData, TWData>>> get_plc_array, ushort position, bool reading)
        {
            var array = get_plc_array(Flux.ConnectionProvider.VariableStore);
            if (array.HasValue)
                LogMessage(array.Value, position, reading);
        }

        public FileResponse LogMessage(FileResponse file_response, IFluxMCodeStorageViewModel file, Optional<Exception> ex = default)
        {
            var title = "Gestione dei file";
            var mcode_name = file.Analyzer.MCode.Name;
            switch (file_response)
            {
                case FileResponse.FILE_DELETED:
                    LogMessage(title, $"File cancellato: {mcode_name}", MessageLevel.INFO, 23001);
                    break;
                case FileResponse.FILE_DELETE_ERROR:
                    LogMessage(title, $"Impossibile cancellare: {mcode_name} {(ex.HasValue ? $"ex: ({ex.Value.Message})" : "")}", MessageLevel.ERROR, 23002);
                    break;
            }
            return file_response;
        }
        public NetResponse LogMessage(NetResponse net_response, Optional<Exception> ex = default)
        {
            var title = "Comunicazione di rete";
            switch (net_response)
            {
                case NetResponse.UDP_DISCOVERY_EXCEPTION:
                    LogMessage(title, $"Discovery UDP non inizializzata {(ex.HasValue ? $"ex: ({ex.Value.Message})" : "")}", MessageLevel.ERROR, 22001);
                    break;
                case NetResponse.PUT_SETTINGS_EXCEPTION:
                    LogMessage(title, $"Errore caricamento impostazioni {(ex.HasValue ? $"ex: ({ex.Value.Message})" : "")}", MessageLevel.ERROR, 22002);
                    break;
                case NetResponse.PUT_MCODE_EXCEPTION:
                    LogMessage(title, $"Errore caricamento mcode {(ex.HasValue ? $"ex: ({ex.Value.Message})" : "")}", MessageLevel.ERROR, 22003);
                    break;
            }
            return net_response;
        }
        public DatabaseResponse LogMessage(DatabaseResponse database_response, Optional<ServiceStatus> service_status = default, Optional<Exception> ex = default)
        {
            var title = "Inizializzazione Database";
            switch (database_response)
            {
                case DatabaseResponse.DATABASE_DOWNLOAD_NOT_INITIALIZED:
                    LogMessage(title, $"Impossibile iniziare il download {(service_status.HasValue ? $"status: ({service_status.Value})" : "")}", MessageLevel.ERROR, 21001);
                    break;
                case DatabaseResponse.DATABASE_DOWNLOAD_NOT_COMPLETED:
                    LogMessage(title, $"Impossibile terminare il download {(service_status.HasValue ? $"status: ({service_status.Value})" : "")}", MessageLevel.ERROR, 21002);
                    break;
                case DatabaseResponse.DATABASE_DOWNLOAD_EXCEPTION:
                    LogMessage(title, $"Eccezione durante il download {(ex.HasValue ? $"ex: ({ex.Value.Message})" : "")}", MessageLevel.ERROR, 21003);
                    break;
                case DatabaseResponse.DATABASE_LOAD_EXCEPTION:
                    LogMessage(title, $"Eccezione durante il caricamento {(ex.HasValue ? $"ex: ({ex.Value.Message})" : "")}", MessageLevel.ERROR, 21005);
                    break;
                case DatabaseResponse.DATABASE_NOT_FOUND:
                    LogMessage(title, $"Database non trovato", MessageLevel.ERROR, 21004);
                    break;
            }
            return database_response;
        }

        public EToolUpdateResult LogMessage(EToolUpdateResult update_tool)
        {
            var title = "AGGIORNAMENTO UTENSILE";
            switch (update_tool)
            {
                case EToolUpdateResult.TOOL_CHANGE_SUCCESS:
                    LogMessage(title, $"SUCCESSO", MessageLevel.DEBUG, 27000);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_ERROR:
                    LogMessage(title, $"ERRORE", MessageLevel.ERROR, 27001);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_CANCELLED:
                    LogMessage(title, $"CAMBIO UTENSILE CANCELLATO", MessageLevel.INFO, 27002);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_IDLE_ERROR:
                    LogMessage(title, $"PLC NON IN IDLE", MessageLevel.ERROR, 27003);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_DATABASE_ERROR:
                    LogMessage(title, $"DATABASE NON TROVATO", MessageLevel.ERROR, 27004);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_PARAMACRO_ERROR:
                    LogMessage(title, $"PARAMACRO NON ESEGUITA", MessageLevel.ERROR, 27005);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_READ_TOOL_ERROR:
                    LogMessage(title, $"LETTURA DELL'UTENSILE NON VALIDA", MessageLevel.ERROR, 27006);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_WRITE_TOOL_ERROR:
                    LogMessage(title, $"SCRITTURA DELL'UTENSILE NON VALIDA", MessageLevel.ERROR, 27007);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_SEARCH_TOOL_ERROR:
                    LogMessage(title, $"RICERCA DELL'UTENSILE NON VALIDA", MessageLevel.ERROR, 27008);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_SELECT_TOOL_ERROR:
                    LogMessage(title, $"SELEZIONE DELL'UTENSILE NON VALIDA", MessageLevel.ERROR, 27009);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_READ_NOZZLE_ERROR:
                    LogMessage(title, $"LETTURA DELL'UGELLO NON VALIDA", MessageLevel.ERROR, 27010);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_WRITE_NOZZLE_ERROR:
                    LogMessage(title, $"SCRITTURA DELL'UGELLO NON VALIDA", MessageLevel.ERROR, 27011);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_SEARCH_NOZZLE_ERROR:
                    LogMessage(title, $"RICERCA DELL'UGELLO NON VALIDA", MessageLevel.ERROR, 27012);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_SELECT_NOZZLE_ERROR:
                    LogMessage(title, $"SELEZIONE DELL'UGELLO NON VALIDA", MessageLevel.ERROR, 27013);
                    break;
                case EToolUpdateResult.TOOL_CHANGE_INVALID_READER_POSITION:
                    LogMessage(title, $"POSIZIONE DEL LETTORE NON VALIDA", MessageLevel.ERROR, 27014);
                    break;
            }
            return update_tool;
        }

        public NFCReaderResponse LogMessage(
            NFCReaderResponse nfc_response,
            Optional<int> position = default,
            Optional<UFR_STATUS> dl_status = default,
            Optional<string> optional_args = default)
        {
            var title = "Lettura NFC";
            switch (nfc_response)
            {
                case NFCReaderResponse.NFC_GET_SUCCESS:
                    log_message(title, $"Lettore trovato", MessageLevel.DEBUG, 19000);
                    break;
                case NFCReaderResponse.NFC_READ_SUCCESS:
                    log_message(title, $"Lettura NFC", MessageLevel.DEBUG, 19001);
                    break;
                case NFCReaderResponse.NFC_WRITE_SUCCESS:
                    log_message(title, $"Scrittura NFC", MessageLevel.DEBUG, 19002);
                    break;
                case NFCReaderResponse.NFC_INVALID_SETTINGS:
                    log_message(title, $"Impostazioni nfc non corrette", MessageLevel.ERROR, 19003);
                    break;
                case NFCReaderResponse.NFC_INVALID_POSITION:
                    log_message(title, $"Posizione nfc non corretta", MessageLevel.ERROR, 19004);
                    break;
                case NFCReaderResponse.NFC_INVALID_READING:
                    log_message(title, $"Errore di lettura", MessageLevel.ERROR, 19004);
                    break;
                case NFCReaderResponse.NFC_INVALID_WRITING:
                    log_message(title, $"Errore di scrittura", MessageLevel.ERROR, 19005);
                    break;
                case NFCReaderResponse.NFC_NO_DOCUMENT:
                    log_message(title, $"Nessun documento trovato", MessageLevel.ERROR, 19006);
                    break;
                case NFCReaderResponse.NFC_NO_TAG:
                    log_message(title, $"Nessun tag trovato", MessageLevel.ERROR, 19007);
                    break;
                case NFCReaderResponse.NFC_INVALID_COMMUNICATION:
                    log_message(title, $"Comunicazione col tag non corretta", MessageLevel.ERROR, 19008);
                    break;
            }
            return nfc_response;

            void log_message(string title, string body, MessageLevel lvl, int code)
            {
                var sb = new StringBuilder();
                sb.AppendLine(body);
                if (position.HasValue)
                    sb.AppendLine($"pos: {position.Value}");
                if (dl_status.HasValue)
                    sb.AppendLine($"status: {dl_status.Value}");
                if (optional_args.HasValue)
                    sb.AppendLine(optional_args.Value);
                LogMessage(title, body, lvl, code);
            }
        }

        public NFCReaderResponse LogMessage<TNFCTag>(
           NFCReaderResponse nfc_response,
           Optional<NFCReading<TNFCTag>> reading,
           Optional<int> position = default,
           Optional<UFR_STATUS> dl_status = default)
           where TNFCTag : INFCTag
        {
            var sb = new StringBuilder();
            if (reading.HasValue)
            {
                sb.AppendLine($"id: {reading.Value.CardId}");
                sb.AppendLine($"tag: {reading.Value.Tag}");
            }

            LogMessage(nfc_response, position, dl_status, sb.ToString());

            return nfc_response;
        }

        public PLCHoldResult LogMessage(PLCHoldResult hold_result)
        {
            var title = "Pausa PLC";
            switch (hold_result)
            {
                case PLCHoldResult.NONE:
                    LogMessage(title, "Risultato nullo", MessageLevel.ERROR, 20001);
                    break;
                case PLCHoldResult.WAIT_ERROR:
                    LogMessage(title, "Errore di attesa", MessageLevel.ERROR, 20002);
                    break;
                case PLCHoldResult.HOLD_ERROR:
                    LogMessage(title, "Errore di pausa", MessageLevel.ERROR, 20003);
                    break;
                case PLCHoldResult.HOLD_CANCELED:
                    LogMessage(title, "Pausa cancellata", MessageLevel.INFO, 20004);
                    break;
                case PLCHoldResult.SUCCESS:
                    LogMessage(title, "Pausa eseguita", MessageLevel.DEBUG, 20000);
                    break;
            }
            return hold_result;
        }
        public HomeResponse LogMessage(HomeResponse home_result)
        {
            var title = "Homing Assi";
            switch (home_result)
            {
                case HomeResponse.HOME_ERROR_CANCELED:
                    LogMessage(title, "Homing annullato", MessageLevel.INFO, 18001);
                    break;
                case HomeResponse.HOME_ERROR_PLC_REFUSED:
                    LogMessage(title, "Impossibile ottenere il consenso del PLC", MessageLevel.ERROR, 18002);
                    break;
                case HomeResponse.HOME_ERROR_DRIVE_DISABLED:
                    LogMessage(title, "Impossibile abilitare i motori", MessageLevel.ERROR, 18003);
                    break;
                case HomeResponse.HOME_ERROR_EXEC_FAULT:
                    LogMessage(title, "Stringa mdi non eseguita", MessageLevel.ERROR, 18004);
                    break;
                case HomeResponse.HOME_ERROR_NOT_HOMED:
                    LogMessage(title, "Stampante non azzerata", MessageLevel.ERROR, 18005);
                    break;
                case HomeResponse.HOME_ERROR_NOT_FINALIZED:
                    LogMessage(title, "Finalizzazione non avvenuta", MessageLevel.ERROR, 18006);
                    break;
                case HomeResponse.HOME_SUCCESS:
                    LogMessage(title, "Homing eseguito", MessageLevel.DEBUG, 18000);
                    break;
            }
            return home_result;
        }
        public PLCResetResponse LogMessage(PLCResetResponse reset_result)
        {
            var title = "Reset PLC";
            switch (reset_result)
            {
                case PLCResetResponse.NONE:
                    LogMessage(title, "Risultato nullo", MessageLevel.ERROR, 10001);
                    break;
                case PLCResetResponse.WAIT_ERROR:
                    LogMessage(title, "Errore di attesa", MessageLevel.ERROR, 10002);
                    break;
                case PLCResetResponse.RESET_ERROR:
                    LogMessage(title, "Errore di reset", MessageLevel.ERROR, 10003);
                    break;
                case PLCResetResponse.LOWER_PLATE_ERROR:
                    LogMessage(title, "Errore di abbassa piatto", MessageLevel.ERROR, 10004);
                    break;
                case PLCResetResponse.RESET_CANCELED:
                    LogMessage(title, "Hold cancellato", MessageLevel.INFO, 10005);
                    break;
                case PLCResetResponse.RESET_SUCCESS:
                    LogMessage(title, "Reset eseguito", MessageLevel.DEBUG, 10000);
                    break;
            }
            return reset_result;
        }
        public PLCStartResult LogMessage(PLCStartResult start_result)
        {
            var title = "Cycle PLC";
            switch (start_result)
            {
                case PLCStartResult.NONE:
                    LogMessage(title, "Risultato nullo", MessageLevel.ERROR, 30001);
                    break;
                case PLCStartResult.PROCESS_PLC_STATUS_ERROR:
                    LogMessage(title, "Errore di status", MessageLevel.ERROR, 30002);
                    break;
                case PLCStartResult.HRUN_ERROR:
                    LogMessage(title, "Errore di hrun", MessageLevel.ERROR, 30003);
                    break;
                case PLCStartResult.RESET_ERROR:
                    LogMessage(title, "Errore di reset", MessageLevel.ERROR, 30004);
                    break;
                case PLCStartResult.CYCLE_ERROR:
                    LogMessage(title, "Errore di cycle", MessageLevel.ERROR, 30005);
                    break;
                case PLCStartResult.PROCESS_MODE_ERROR:
                    LogMessage(title, "Errore di modalità del processo", MessageLevel.ERROR, 30006);
                    break;
                case PLCStartResult.PROCESS_START_STATUS_ERROR:
                    LogMessage(title, "Errore di stato iniziale", MessageLevel.ERROR, 30007);
                    break;
                case PLCStartResult.PART_PROGRAM_NOT_FOUND_ERROR:
                    LogMessage(title, "Part program non trovato", MessageLevel.ERROR, 30008);
                    break;
                case PLCStartResult.INVALID_START_EVALUATION:
                    LogMessage(title, "Valutazione di stampa non valida", MessageLevel.ERROR, 30009);
                    break;
                case PLCStartResult.INVALID_MCODE_ERROR:
                    LogMessage(title, "Programma non selezionato", MessageLevel.ERROR, 300010);
                    break;
                case PLCStartResult.SUCCESS:
                    LogMessage(title, "Cycle eseguito", MessageLevel.DEBUG, 30000);
                    break;
            }
            return start_result;
        }
        public OSAI_CloseResponse LogMessage(OSAI_CloseResponse response)
        {
            var title = "Disconnessione PLC";
            switch (response)
            {
                case OSAI_CloseResponse.CLOSE_INVALID_STATE:
                    LogMessage(title, "Stato non valido", MessageLevel.ERROR, 40001);
                    break;
                case OSAI_CloseResponse.CLOSE_EXCEPTION:
                    LogMessage(title, "Stato non valido", MessageLevel.ERROR, 40002);
                    break;
                case OSAI_CloseResponse.CLOSE_SUCCESS:
                    LogMessage(title, "Successo", MessageLevel.DEBUG, 40000);
                    break;
            }
            return response;
        }
        public OSAI_SetupResponse LogMessage(OSAI_SetupResponse setup_result)
        {
            var title = "Inizializzazione PLC";
            switch (setup_result)
            {
                case OSAI_SetupResponse.SETUP_RESET_ERROR:
                    LogMessage(title, "Errore durante il reset", MessageLevel.ERROR, 60001);
                    break;
                case OSAI_SetupResponse.SETUP_RUN_ERROR:
                    LogMessage(title, "Errore durante la fase di RUN", MessageLevel.ERROR, 60002);
                    break;
                case OSAI_SetupResponse.SETUP_IDLE_WAIT_ERROR:
                    LogMessage(title, "Errore durante la fase di idle", MessageLevel.ERROR, 60003);
                    break;
                case OSAI_SetupResponse.SETUP_STATUS_ERROR:
                    LogMessage(title, "Errore durante la richiesta di status", MessageLevel.ERROR, 60004);
                    break;
                case OSAI_SetupResponse.SETUP_REF_ERROR:
                    LogMessage(title, "Errore durante il riferimento assi", MessageLevel.ERROR, 60005);
                    break;
                case OSAI_SetupResponse.SETUP_AUX_ERROR:
                    LogMessage(title, "Errore durante la abilitazione aux", MessageLevel.ERROR, 60006);
                    break;
                case OSAI_SetupResponse.SETUP_RUN_WAIT_ERROR:
                    LogMessage(title, "Errore durante il reset", MessageLevel.ERROR, 60007);
                    break;
                case OSAI_SetupResponse.SETUP_AUTO_ERROR:
                    LogMessage(title, "Errore durante la richiesta di AUTO", MessageLevel.ERROR, 60008);
                    break;
                case OSAI_SetupResponse.SETUP_AUTO_WAIT_ERROR:
                    LogMessage(title, "Errore durante la fase di AUTO", MessageLevel.ERROR, 60009);
                    break;
                case OSAI_SetupResponse.SETUP_SUCCESS:
                    LogMessage(title, "PLC Inizializzato", MessageLevel.DEBUG, 60000);
                    break;
            }
            return setup_result;
        }
        public OSAI_ConnectResponse LogMessage(OSAI_ConnectResponse connect_result)
        {
            var title = "Connessione TCP PLC";
            switch (connect_result)
            {
                case OSAI_ConnectResponse.CONNECT_EXCEPTION:
                    LogMessage(title, "Errore durante la connessione", MessageLevel.ERROR, 70001);
                    break;
                case OSAI_ConnectResponse.CONNECT_INVALID_ADDRESS:
                    LogMessage(title, "Indirizzo non valido", MessageLevel.ERROR, 70002);
                    break;
                case OSAI_ConnectResponse.CONNECT_INVALID_STATE:
                    LogMessage(title, "Stato non valido", MessageLevel.ERROR, 70003);
                    break;
                case OSAI_ConnectResponse.CONNECT_SUCCESS:
                    LogMessage(title, "PLC Connesso", MessageLevel.DEBUG, 70000);
                    break;
            }
            return connect_result;
        }
        public CommunicationState LogMessage(Optional<CommunicationState> state)
        {
            var title = "Connessione WebService PLC";
            if (!state.HasValue)
                LogMessage(title, "OPENClient non aperto", MessageLevel.ERROR, 50001);

            switch (state.Value)
            {
                case CommunicationState.Closed:
                    LogMessage(title, "OPENClient chiuso", MessageLevel.ERROR, 50002);
                    break;
                case CommunicationState.Closing:
                    LogMessage(title, "OPENClient in chiusura", MessageLevel.ERROR, 50003);
                    break;
                case CommunicationState.Faulted:
                    LogMessage(title, "OPENClient in errore", MessageLevel.ERROR, 50004);
                    break;
                case CommunicationState.Opening:
                    LogMessage(title, "OPENClient in apertura", MessageLevel.ERROR, 50005);
                    break;
                case CommunicationState.Created:
                    LogMessage(title, "OPENClient in creazione", MessageLevel.ERROR, 50006);
                    break;
                case CommunicationState.Opened:
                    LogMessage(title, "OPENClient connesso", MessageLevel.DEBUG, 50000);
                    break;
            }

            return state.ValueOr(() => CommunicationState.Faulted);
        }
        public OSAI_ExecuteMDIResponse LogMessage(OSAI_ExecuteMDIResponse mdi_result)
        {
            var title = "Esecuzione stringa MDI";
            switch (mdi_result)
            {
                case OSAI_ExecuteMDIResponse.EXEC_MDI_STATUS_ERROR:
                    LogMessage(title, $"Errore di stato PLC", MessageLevel.ERROR, 80001);
                    break;
                case OSAI_ExecuteMDIResponse.EXEC_MDI_MODE_ERROR:
                    LogMessage(title, $"Impossibile impostare il PLC su MDI", MessageLevel.ERROR, 80002);
                    break;
                case OSAI_ExecuteMDIResponse.EXEC_MDI_STRING_ERROR:
                    LogMessage(title, $"Impossibile impostare la stringa MDI", MessageLevel.ERROR, 80003);
                    break;
                case OSAI_ExecuteMDIResponse.EXEC_MDI_CYCLE_ERROR:
                    LogMessage(title, $"Impossibile impostare il PLC su Cycle", MessageLevel.ERROR, 80004);
                    break;
                case OSAI_ExecuteMDIResponse.EXEC_MDI_WAIT_MODE_ERROR:
                    LogMessage(title, $"Timeout modalità PLC", MessageLevel.ERROR, 80005);
                    break;
                case OSAI_ExecuteMDIResponse.EXEC_MDI_WAIT_STATUS_ERROR:
                    LogMessage(title, $"Timeout stato PLC", MessageLevel.ERROR, 80006);
                    break;
                case OSAI_ExecuteMDIResponse.EXEC_MDI_SUCCESS:
                    LogMessage(title, $"Stringa MDI eseguita", MessageLevel.DEBUG, 80000);
                    break;
            }
            return mdi_result;
        }

        internal UFR_STATUS LogMessage(UFR_STATUS dl_status, MessageLevel level)
        {
            var title = "ERRORE NFC";
            LogMessage(title, $"{dl_status}", level, (ushort)dl_status);
            return dl_status;
        }

        public PLCModeResult LogMessage(PLCModeResult mode_result, OSAI_ProcessMode mode)
        {
            var title = "Selezione della modalità PLC";
            switch (mode_result)
            {
                case PLCModeResult.NONE:
                    LogMessage(title, $"Risultato nullo", MessageLevel.ERROR, 90001);
                    break;
                case PLCModeResult.ERROR:
                    LogMessage(title, $"Impossibile selezionare la modalità \"{mode}\"", MessageLevel.ERROR, 90002);
                    break;
                case PLCModeResult.SUCCESS:
                    LogMessage(title, $"Selezionata la modalità \"{mode}\"", MessageLevel.DEBUG, 90000);
                    break;
            }
            return mode_result;
        }

        public MaterialChangeResult LogMessage(MaterialChangeResult filament_response, Optional<MaterialState> mode = default)
        {
            var title = $"Operazione sul filamento: {mode}";
            switch (filament_response)
            {
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_TOOL_MATERIAL:
                    LogMessage(title, "Profilo di stampa non valido", MessageLevel.ERROR, 14001);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP:
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_EXTRUSION_TEMP:
                    LogMessage(title, "Temperatura di estrusione non valida", MessageLevel.ERROR, 14002);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO:
                    LogMessage(title, "Errore paramacro", MessageLevel.ERROR, 14003);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_STATUS:
                    LogMessage(title, "Stato non valido", MessageLevel.ERROR, 14004);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR:
                    LogMessage(title, "Errore di cambio", MessageLevel.ERROR, 14005);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_NOZZLE:
                    LogMessage(title, "Estrusore non valido", MessageLevel.ERROR, 14006);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_SUCCESS:
                    LogMessage(title, "Operazione terminata", MessageLevel.DEBUG, 14000);
                    break;
                case MaterialChangeResult.MATERIAL_CHANGE_CANCELLED:
                    LogMessage(title, "Operazione annullata", MessageLevel.INFO, 14000);
                    break;
            }
            return filament_response;
        }
        public ToolManteinanceResult LogMessage(ToolManteinanceResult tool_response, Optional<ToolNozzleState> mode = default)
        {
            var title = $"Operazione sul tool: {mode}";
            switch (tool_response)
            {
                case ToolManteinanceResult.TOOL_MAINTENANCE_ERROR_PARAMACRO:
                    LogMessage(title, "Errore paramacro", MessageLevel.ERROR, 28001);
                    break;
                case ToolManteinanceResult.TOOL_MAINTENANCE_ERROR_INVALID_NOZZLE:
                    LogMessage(title, "Errore paramacro", MessageLevel.ERROR, 28002);
                    break;
                case ToolManteinanceResult.TOOL_MAINTENANCE_ERROR_INVALID_STATUS:
                    LogMessage(title, "Stato non valido", MessageLevel.ERROR, 28003);
                    break;
                case ToolManteinanceResult.TOOL_MAINTENANCE_SUCCESS:
                    LogMessage(title, "Operazione terminata", MessageLevel.DEBUG, 28000);
                    break;
            }
            return tool_response;
        }
        public void LogError<TCaller>(TCaller caller, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string member = null)
        {
            LogMessage($"Error", $"From: {typeof(TCaller).Name}.{member} at line: {lineNumber}", MessageLevel.ERROR, DateTime.Now, 16002);
        }
        public void LogException<TCaller>(TCaller caller, Exception ex, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string callerMember = null)
        {
            LogMessage($"Exception from {typeof(TCaller).Name}.{callerMember} at line: {lineNumber}", ex.Message, MessageLevel.ERROR, DateTime.Now, 16001);
        }
        public (PLCProcessDataResult result, Optional<OSAI_Process> data) LogMessage(PLCProcessDataResult process_result, Optional<OSAI_Process> process_status)
        {
            var title = "Lettura dati di processo";
            switch (process_result)
            {
                case PLCProcessDataResult.NONE:
                    LogMessage(title, "Risultato nullo", MessageLevel.ERROR, 15001);
                    break;
                case PLCProcessDataResult.ERROR:
                    LogMessage(title, "Errore lettura dati processo PLC", MessageLevel.ERROR, 15002);
                    break;
                case PLCProcessDataResult.SUCCESS:
                    LogMessage(title, "Dati di processo letti", MessageLevel.DEBUG, 15003);
                    break;
            }
            return (process_result, process_status);
        }

        public PLCUpdateResponse LogMessage(PLCUpdateResponse plc_update, Optional<Exception> ex = default)
        {
            var title = "Aggirnamento PLC";
            switch (plc_update)
            {
                case PLCUpdateResponse.PLC_UPDATE_STATUS_ERROR:
                    LogMessage(title, "Impossibile aggiornare lo stato", MessageLevel.ERROR, 20001);
                    break;
                case PLCUpdateResponse.PLC_UPDATE_AXIS_ERROR:
                    LogMessage(title, "Impossibile leggere gli assi", MessageLevel.ERROR, 20002);
                    break;
                case PLCUpdateResponse.PLC_UPDATE_MARKER_ERROR:
                    LogMessage(title, "Impossibile leggere il marker", MessageLevel.ERROR, 20003);
                    break;
                case PLCUpdateResponse.PLC_UPDATE_EXCEPTION:
                    LogMessage(title, $"Eccezione: {(ex.HasValue ? ex.Value.Message : "")}", MessageLevel.ERROR, 20004);
                    break;
            }
            return plc_update;
        }

        public StatusBarState LogMessage(string title, int error, StatusBarState status)
        {
            switch (status)
            {
                case StatusBarState.Stable:
                    LogMessage(title, $"{title} stabile", MessageLevel.DEBUG, error + 0);
                    break;
                case StatusBarState.Disabled:
                    LogMessage(title, $"{title} non attivo", MessageLevel.DEBUG, error + 1);
                    break;
                case StatusBarState.Warning:
                    LogMessage(title, $"Attenzione: {title}", MessageLevel.WARNING, error + 2);
                    break;
                case StatusBarState.Error:
                    LogMessage(title, $"{title} in errore", MessageLevel.ERROR, error + 3);
                    break;
            }
            return status;
        }
    }
}
