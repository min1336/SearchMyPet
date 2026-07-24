using System;

namespace CamoHuntAR
{
    public sealed class CamouflagePaintHistory
    {
        private CamouflageData _snapshot;

        public bool CanUndo => _snapshot != null;

        public void Capture(CamouflageData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _snapshot = data.Clone();
        }

        public bool TryUndo(out CamouflageData data)
        {
            if (_snapshot == null)
            {
                data = null;
                return false;
            }

            data = _snapshot.Clone();
            _snapshot = null;
            return true;
        }

        public void Clear()
        {
            _snapshot = null;
        }
    }
}
