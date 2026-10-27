namespace TheRavine.ObjectControl
{
    public static class ConstructionService
    {
        public static bool TryRequestPlacement(long position, ObjectInfo info)
        {
            RavineLog.Info($"[ConstructionService] Construction3D не реализован: id={(info != null ? info.Id : -1)}, pos={position}");
            return false;
        }
    }
}
