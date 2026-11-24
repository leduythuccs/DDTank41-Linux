

















-- =============================================
-- Author:		<XiaoV>
-- Create date: <2009-09-25>
-- Description:	<用户提交后批量修改用户名与公会>
-- =============================================
-- Rewritten for the MariaDB translator: the EXEC('...' + @Db_A + '...') strings (which only spliced in
-- this database's own name) are written out as statements.
CREATE PROCEDURE [dbo].[Sp_Renames_Batch]

AS
BEGIN

/*第一步修改用户昵称*/
  /*第一：将需要变更昵称的用户列出来*/
  Create Table #Temp_User(UserId Int,NickName nvarchar(50))    
  Insert Into  #Temp_User
   SELECT B.UserID,B.NickName FROM ( SELECT UserName,MAX(ID) ID FROM Rename_Nick 
         WHERE IsExist=1  GROUP BY UserName )A
               LEFT OUTER JOIN Rename_Nick B
                    ON A.ID=B.ID
  Print '1'

  /*第二：更新Consortia的CreatorName、ChairmanName*/  
  Update A Set A.CreatorName=B.NickName From Consortia A ,#Temp_User B
         Where A.CreatorID=B.UserId
  
  Update A Set A.ChairmanName=B.NickName From Consortia A,#Temp_User B
         Where A.ChairmanID=B.UserId
  Print '2'

  /*第三：更新Consortia_Apply_Users的UserName*/
  Update A Set A.UserName=B.NickName From Consortia_Apply_Users A ,#Temp_User B
         Where A.UserID=B.UserId
  Print '3' 

  /*第四：更新Consortia_Invite_Users的UserName、InviteName*/      
  Update A Set A.UserName=B.NickName From Consortia_Invite_Users A,#Temp_User B
         Where A.UserID=B.UserId

  Update A Set A.InviteName=B.NickName From Consortia_Invite_Users A,#Temp_User B
         Where A.InviteID=B.UserId
  Print '4'

  /*第五：更新Consortia_Users的UserName	RatifierName*/  
  Update A Set A.UserName=B.NickName From Consortia_Users A ,#Temp_User B 
         Where A.UserID=B.UserId

  Update A Set A.RatifierName=B.NickName From Consortia_Users A,#Temp_User B
         Where A.RatifierID=B.UserId
  Print '5'

  /*第六：Marry_Apply的ApplyUserName*/
  Update A Set A.ApplyUserName=B.NickName From Marry_Apply A,#Temp_User B
         Where A.ApplyUserID=B.UserId
  Print '6'  

  /*第七：Marry_Room_Info的PlayerName、GroomName、BrideName*/
  Update A Set A.PlayerName=B.NickName From Marry_Room_Info A,#Temp_User B
         Where A.PlayerID=B.UserId

  Update A Set A.GroomName=B.NickName From Marry_Room_Info A,#Temp_User B
         Where A.GroomID=B.UserId
  

  Update A Set A.BrideName=B.NickName From Marry_Room_Info A,#Temp_User B
         Where A.BrideID=B.UserId
 
  Print '7'

  /*第八:User_Messages的Receiver、Sender*/
  Update A Set A.Receiver=B.NickName From User_Messages A,#Temp_User B
         Where A.ReceiverID=B.UserId
  
  Update A Set A.Sender=B.NickName From User_Messages A,#Temp_User B
         Where A.SenderID=B.UserId
  Print '8'

  /*第九：更新更新Sys_Users_Detail的SpouseName*/
  Update A Set A.SpouseName=B.NickName From Sys_Users_Detail A,#Temp_User B
        Where A.SpouseID=B.UserId

  Update A Set A.NickName=B.NickName From Sys_Users_Detail A,#Temp_User B
        Where A.UserId=B.UserId
  Print '9'

  /*第十:更新Auction的AuctioneerName、BuyerName*/
  Update A Set A.AuctioneerName=B.NickName From Auction A,#Temp_User B
        Where A.AuctioneerID=B.UserId
  Print '10'

  Update A Set A.BuyerName=B.NickName From Auction A,#Temp_User B
        Where A.BuyerID=B.UserId
  Print '11'

 
  /*第十：清除批处理数据*/
  Update  Rename_Nick Set IsExist=0  Where IsExist=1
  Drop Table #Temp_User

/*第二步：修改工会昵称*/
  Create Table #Temp_Consortia(ConsortiaID Int,ConsortiaName nvarchar(200))    
  Insert Into #Temp_Consortia
  Select ConsortiaID,ConsortiaName From  Rename_Consortia  Where  IsExist=1

  Print '12'

  /*第一：Consortia_Apply_Users的ConsortiaName*/
  Update A Set A.ConsortiaName=B.ConsortiaName From Consortia_Apply_Users A,#Temp_Consortia B
         Where A.ConsortiaID=B.ConsortiaID
  Print '13'
  
  /*第二：Consortia_Invite_Users的ConsortiaName*/
  Update A Set A.ConsortiaName=B.ConsortiaName From Consortia_Invite_Users A,#Temp_Consortia B
         Where A.ConsortiaID=B.ConsortiaID
  Print '14'
   
  /*第三：Consortia的ConsortiaName*/
  Update A Set A.ConsortiaName=B.ConsortiaName From Consortia A,#Temp_Consortia B
         Where A.ConsortiaID=B.ConsortiaID
  Print '15'

  /*清除批处理数据*/
  Update  Rename_Consortia Set IsExist=0  Where IsExist=1
  Drop Table #Temp_Consortia
END






