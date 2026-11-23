namespace Fighting.Server.Rooms
{
    public class AddRoomAction : IAction
    {
        private ProxyRoom m_room;

        public AddRoomAction(ProxyRoom room)
        {
			m_room = room;
        }

        public void Execute()
        {
			ProxyRoomMgr.AddRoomUnsafe(m_room);
			if (m_room.startWithNpc)
			{
				ProxyRoomMgr.StartWithNpcUnsafe(m_room);
			}
			else if (m_room.isAutoBot)
			{
				ProxyRoomMgr.StartBotWithWaitingRoomUnsafe(m_room);
			}
			else
			{
				ProxyRoomMgr.RequestAutoBotIfNoWaitUnsafe(m_room);
			}
        }
    }
}
