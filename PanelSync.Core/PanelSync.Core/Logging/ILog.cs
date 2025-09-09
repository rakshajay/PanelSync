// ILog.cs
using System;

namespace PanelSync.Core.Logging
{
    //[08/27/2025]:Raksha- Lightweight logging contract for Core.
    public interface ILog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex = null); //[08/27/2025]:Raksha- no nullable '?'
        void Debug(string message);
    }
}
