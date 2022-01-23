namespace Flux.ViewModels
{
    public enum MsgHistoryReadMode
    {
        Absolute = 0,
        CurrentBoot = 1,
    }

    public enum MsgHistorySource
    {
        Log = 0,
        Memo = 1
    }


    /*public class MessageCounter
    {
        public uint EmergencyCounter { get; }
        public uint AnomalyCounter { get; }
        public uint ErrorCounter { get; }
        public uint MemoCounter { get; }
        public uint LogCounter { get; }
        public MessageCounter(unsignedintarray values)
        {
            EmergencyCounter = values[0];
            AnomalyCounter = values[3];
            ErrorCounter = values[1];
            MemoCounter = values[4];
            LogCounter = values[2];
        }
        public MessageCounter(IMessageHistory prev, IMessageHistory cur)
        {
            EmergencyCounter = GetDiff(c => c.EmergencyCounter);
            AnomalyCounter = GetDiff(c => c.AnomalyCounter);
            ErrorCounter = GetDiff(c => c.ErrorCounter);
            MemoCounter = GetDiff(c => c.MemoCounter);
            LogCounter = GetDiff(c => c.LogCounter);

            uint GetDiff(Func<MessageCounter, uint> get_counter)
            {
                var prev_counter = get_counter(prev.Counter);
                var cur_counter = get_counter(cur.Counter);
                return prev_counter <= cur_counter ? cur_counter - prev_counter : cur_counter;
            }
        }
    }

    public class MessageHistoryCounter
    {
        public MessageCounter AllBootMsgCounter { get; }
        public MessageCounter ThisBootMsgCounter { get; }
        public MessageHistoryCounter(CheckHistoryResponse history)
        {
            AllBootMsgCounter = new MessageCounter(history.AllBootMsgCounter);
            ThisBootMsgCounter = new MessageCounter(history.ThisBootMsgCounter);
        }
    }*/
}
