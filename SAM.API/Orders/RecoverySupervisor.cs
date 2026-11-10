using System;
using System.Diagnostics;

namespace SAM.API.Orders
{
    internal static class RecoverySupervisor
    {
        // Match the start time as well as the PID: Windows can reuse process IDs.
        internal static bool WaitForCrash(int processId, long startTicks)
        {
            using var owner = Process.GetProcessById(processId);
            if (owner.StartTime.ToUniversalTime().Ticks != startTicks) return false;
            owner.WaitForExit();
            return owner.ExitCode != 0;
        }
    }
}
