namespace Game.Server.RingStation
{
    public class VirtualPlayerInfo
    {
        public string Style { get; set; }
        public int Weapon { get; set; }
        /// <summary>true = male. The outfit in Style is picked for this sex.</summary>
        public bool Sex { get; set; }
    }
}