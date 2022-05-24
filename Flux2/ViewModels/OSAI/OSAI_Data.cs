using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json;
using OSAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;

namespace Flux.ViewModels
{

    // 1 : Programmed position
    // 2 : Interpolated position
    // 3 : Transducer position
    // 4 : Followinf error
    // 5 : Distance to go
    // 6 : Interpolated position (Absolute)
    public enum OSAI_AxisPositionSelect : ushort
    {
        Programmed = 1,
        Interpolated = 2,
        Transducer = 3,
        Following = 4,
        Distance = 5,
        Absolute = 6
    }

    //#define IDLE 1 
    //#define CYCLE 2 
    //#define HOLDA 3 
    //#define RUNH 4 
    //#define HRUN 5 
    //#define ERRO 6 
    //#define WAIT 7 
    //#define RESET 8 
    //#define EMERG 9 
    //#define INPUT 10 
    public enum OSAI_ProcessStatus : ushort
    {
        NONE = 0,
        IDLE = 1,
        CYCLE = 2,
        HOLDA = 3,
        RUNH = 4,
        HRUN = 5,
        ERRO = 6,
        WAIT = 7,
        RESET = 8,
        EMERG = 9,
        INPUT = 10
    }

    //#define MDI  1    // MDI Mode (Manual Data Input) 
    //#define AUTO 2    // Automatic Mode  
    //#define SEMI 3    // Semi-Automatic (Block-Block) Mode  
    //#define MANJOG 4  // Continuous Manual Mode  
    //#define INCJOG 5  // Incremental Manual Mode 
    //#define PROFILE 6 // Return to Profile Mode 
    //#define HOME 7    // Axis Reference Mode
    public enum OSAI_ProcessMode : ushort
    {
        NONE = 0,
        MDI = 1,
        AUTO = 2,
        SEMI = 3,
        MANJOG = 4,
        INCJOG = 5,
        PROFILE = 6,
        HOME = 7,
    }

    public enum OSAI_BootMode : ushort
    {
        NONE = 0,
        EMERGENCY = 1,
        RUN = 2,
        SETUP = 3,
        SERVICE = 4
    }

    // 1 =  EMERG_SWITCH_PHASE
    // 2 =  HW_BOOT_PHASE 
    // 3 =  SW_BOOT_PHASE 
    // 4 =  SYSTEM_UP_PHASE
    // 5 =  SERVER_MODE_PHASE
    // 6 =  REMOTE_SETUP_PHASE 
    // 7 =  SERVICE_MODE_PHASE 
    // 8 =  AX_PARAM_VERIFY_PHASE 
    // 15 = PRIMARY_INIT_PHASE
    // 16 = NOT_INIT_PHASE   
    // 17 = SHUTDOWN_PHASE   
    // 19 = ERROR_PHASE
    public enum OSAI_BootPhase : ushort
    {
        EMERG_SWITCH_PHASE = 1,
        HW_BOOT_PHASE = 2,
        SW_BOOT_PHASE = 3,
        SYSTEM_UP_PHASE = 4,
        SERVER_MODE_PHASE = 5,
        REMOTE_SETUP_PHASE = 6,
        SERVICE_MODE_PHASE = 7,
        AX_PARAM_VERIFY_PHASE = 8,
        PRIMARY_INIT_PHASE = 15,
        NOT_INIT_PHASE = 16,
        SHUTDOWN_PHASE = 17,
        ERROR_PHASE = 19
    }

    public enum OSAI_VARCODE : ushort
    {
        NAMED = ushort.MaxValue,
        IW_CODE = 0,
        OW_CODE = 1,
        MW_CODE = 20,
        GW_CODE = 21, // (R)
        SW_CODE = 22,
        PW_CODE = 62,
        MD_CODE = 40,
        GD_CODE = 41, // (R)
        SD_CODE = 42,
        PD_CODE = 43,
        UD_CODE = 44,
        L_CODE = 145, // (R)
        E_CODE = 46,
        SN_CODE = 47,
        H_CODE = 48,
        A_CODE = 45,
        AA_CODE = 28,
        SC_CODE = 50,
        LS_CODE = 18,
        SYMO_D_CODE = 101,
        SYMORET_D_CODE = 104 // (R)
    }

    public enum OSAI_ConnectResponse
    {
        CONNECT_INVALID_ADDRESS,
        CONNECT_INVALID_STATE,
        CONNECT_EXCEPTION,
        CONNECT_SUCCESS,
    }

    public enum OSAI_CloseResponse
    {
        CLOSE_INVALID_STATE,
        CLOSE_EXCEPTION,
        CLOSE_SUCCESS,
    }

    public enum OSAI_ExecuteMDIResponse
    {
        EXEC_MDI_MODE_ERROR,
        EXEC_MDI_STRING_ERROR,
        EXEC_MDI_CYCLE_ERROR,
        EXEC_MDI_WAIT_MODE_ERROR,
        EXEC_MDI_WAIT_STATUS_ERROR,
        EXEC_MDI_SUCCESS,
        EXEC_MDI_STATUS_ERROR
    }

    public enum OSAI_CycleResponse
    {
        CYCLE_EXCEPTION_ERROR,
        CYCLE_REQUEST_ERROR,
        CYCLE_WAIT_ERROR,
        CYCLE_START_ERROR,
        CYCLE_SUCCESS,
    }

    public enum OSAI_SetupResponse
    {
        SETUP_RESET_ERROR,
        SETUP_RUN_ERROR,
        SETUP_IDLE_WAIT_ERROR,
        SETUP_STATUS_ERROR,
        SETUP_REF_ERROR,
        SETUP_AUX_ERROR,
        SETUP_SUCCESS,
        SETUP_RUN_WAIT_ERROR,
        SETUP_AUTO_ERROR,
        SETUP_AUTO_WAIT_ERROR
    }

    [DataContract]
    public struct OSAI_Process
    {
        [DataMember]
        public OSAI_ProcessMode Mode { get; set; }
        [DataMember]
        public OSAI_ProcessStatus Status { get; set; }
        [DataMember]
        public OSAI_ProcessStatus SubStatus { get; set; }

        public OSAI_Process(PROCDATA process_data)
        {
            Mode = (OSAI_ProcessMode)process_data.Mode;
            Status = (OSAI_ProcessStatus)process_data.Status;
            SubStatus = (OSAI_ProcessStatus)process_data.SubStatus;
        }
    }

    [DataContract]
    public struct OSAI_AxisPosition
    {
        [DataMember]
        public char Name { get; set; }
        [DataMember]
        public byte Mode { get; set; }
        [DataMember]
        public float Position { get; set; }
        [DataMember]
        public float TotalOffset { get; set; }

        public OSAI_AxisPosition(GETINTDATA data)
        {
            Name = Convert.ToChar(data.AxisName);
            Mode = data.mode;
            Position = (float)data.position;
            TotalOffset = (float)data.TotalOffset;
        }
    }

    [DataContract]
    public class OSAI_AxisPositionDictionary
    {
        [DataMember]
        [JsonConverter(typeof(JsonConverters.DictionaryConverter<char, OSAI_AxisPosition>))]
        public Dictionary<char, OSAI_AxisPosition> Positions { get; }

        public OSAI_AxisPositionDictionary(GETINTDATAC4array positions)
        {
            var axis_positions = positions.Select(data => new OSAI_AxisPosition(data));
            Positions = axis_positions.ToDictionary(p => p.Name);
        }
        public OSAI_AxisPositionDictionary()
        {
            Positions = new Dictionary<char, OSAI_AxisPosition>();
        }
        public OSAI_AxisPositionDictionary(Dictionary<char, OSAI_AxisPosition> positions)
        {
            Positions = positions;
        }

        [IgnoreDataMember]
        public Vector3 RawPosition
        {
            get
            {
                var x = Positions.Lookup('X').ConvertOr(x => x.Position, () => 0);
                var y = Positions.Lookup('Y').ConvertOr(y => y.Position, () => 0);
                var z = Positions.Lookup('Z').ConvertOr(z => z.Position, () => 0);
                return new Vector3(x, y, z);
            }
        }
        [IgnoreDataMember]
        public Vector3 FromCoreXYPosition
        {
            get
            {
                var core_pos = RawPosition;
                var pos_x = core_pos.X + core_pos.Y;
                var pos_y = core_pos.X - core_pos.Y;
                return new Vector3(pos_x, pos_y, core_pos.Z);
            }
        }

    }

    public class OSAI_MCodeRecovery : IFLUX_MCodeRecovery
    {
        public Guid MCodeGuid { get; }
        public bool IsSelected { get; }
        public uint BlockNumber { get; }
        public short ToolNumber { get; }
        public BlockNumber StartBlock { get; }
        public string FileName => $"{MCodeGuid}.{StartBlock}";
        public Dictionary<string, double> Positions { get; }
        public Dictionary<string, double> Temperatures { get; }

        public OSAI_MCodeRecovery(Guid mcode_guid, BlockNumber start_block, bool is_selected, uint hold_block, short hold_tool, Dictionary<string, double> hold_temp, Dictionary<string, double> hold_pos)
        {
            BlockNumber = hold_block;
            Temperatures = hold_temp;
            IsSelected = is_selected;
            StartBlock = start_block;
            MCodeGuid = mcode_guid;
            ToolNumber = hold_tool;
            Positions = hold_pos;
        }
    }

    // MSGERROR 
    // uint     -> uint/uint    | 1508600 -> 23/1272 Asse %2 (Id %1) non sul profilo
    // uint     -> hex          | 1508600 -> 1704F8
    // hex      -> hex/hex      | 1704F8  -> 17/04F8
    // hex/hex  -> uint/uint    | 17/04F8 -> 23/1272
}
