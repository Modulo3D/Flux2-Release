using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum EConditionState
    {
        Error,
        Warning,
        Stable,
        Disabled,
        Idle,
        Hidden
    }

    [DataContract]
    public struct ConditionState
    {
        public static ConditionState Default { get; } = new ConditionState(EConditionState.Disabled, new RemoteText("", false));

        [DataMember(Name = "valid")]
        public bool Valid { get; set; }

        [DataMember(Name = "state")]
        public EConditionState State { get; set; }

        [DataMember(Name = "message")]
        public RemoteText Message { get; set; }

        [DataMember(Name = "stateBrush")]
        public string StateBrush { get; set; }

        public ConditionState(EConditionState state, RemoteText message)
        {
            State = state;
            Message = message;
            Valid = state switch
            {
                EConditionState.Hidden => true,
                EConditionState.Stable => true,
                _ => false,
            };
            StateBrush = state switch
            {
                EConditionState.Idle => FluxColors.Idle,
                EConditionState.Error => FluxColors.Error,
                EConditionState.Stable => FluxColors.Selected,
                EConditionState.Warning => FluxColors.Warning,
                EConditionState.Disabled => FluxColors.Inactive,
                _ => FluxColors.Error
            };
        }
    }
}