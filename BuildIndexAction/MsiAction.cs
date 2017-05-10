using System;

namespace BuildIndexAction
{
    [Flags]
    public enum MsiAction : byte
    {
        None,
        FirstInstall,
        Upgrading,
        RemovingForUpgrade,
        Maintenance,
        Uninstalling
    }
}