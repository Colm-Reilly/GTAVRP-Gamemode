using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;
using MongoDB.Driver;
using RoleplayServer.resources.core;
using RoleplayServer.resources.database_manager;
using RoleplayServer.resources.player_manager;

namespace RoleplayServer.resources.group_manager
{
    public class GroupManager : Script
    {

        public static List<Group> Groups = new List<Group>();

        public GroupManager()
        {
            DebugManager.DebugMessage("[GroupM] Initalizing group manager...");

            load_groups(); 

            DebugManager.DebugMessage("[GroupM] Group Manager initalized!");

        }

        [Command("listgroups")]
        public void listgroups_cmd(Client player)
        {
            Account acc = API.getEntityData(player.handle, "Account");

            if (acc.AdminLevel < 4)
                return;

            Groups = DatabaseManager.GroupTable.Find(Builders<Group>.Filter.Empty).ToList();
            API.sendChatMessageToPlayer(player, "-----------------------------------------------------------------------------------");
            foreach (var g in Groups)
            {
                switch(g.Type)
                {
                    case 1: API.sendChatMessageToPlayer(player, "Group Name: " + g.Name + " | Type: Faction | Command Type: " + g.CommandType + "."); break;
                    case 2: API.sendChatMessageToPlayer(player, "Group Name: " + g.Name + " | Type: Gang | Command Type: " + g.CommandType + "."); break;
                }
            }
            API.sendChatMessageToPlayer(player, "---------------------------------------------------------------------------------- ");
        }

        [Command("remoteuninvite")]
        public void remoteunivite_cmd(Client player, string playername)
        {
            Character sender = API.getEntityData(player.handle, "Character");

            GroupCommandPermCheck(sender, 6);

            var filter = Builders<Character>.Filter.Eq("CharacterName", playername);
            var foundCharacters = DatabaseManager.CharacterTable.Find(filter).ToList();

            foreach (var c in foundCharacters)
            {
                if (c.GroupId == sender.GroupId)
                {
                    c.GroupId = 0;
                    c.Group = null;
                    c.GroupRank = 0;
                    c.Save();
                }
                API.sendChatMessageToPlayer(player, "You have remote-uninvited " + c.CharacterName + " from the " + sender.Group.Name + ".");
                break;
            }
        }

        [Command("setrank")]
        public void setrank_cmd(Client player, string id, int rank)
        {
            var  receiver = PlayerManager.ParseClient(id);

            if (receiver == null)
            {
                API.sendChatMessageToPlayer(player, Color.White, "That player is not connected.");
                return;
            }

            Character sender = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(sender, 5);

            Character member = API.getEntityData(receiver.handle, "Character");
            if (sender.GroupRank >= member.GroupRank && sender.GroupRank > rank)
            {
                var oldRank = member.GroupRank;
                if (oldRank > rank)
                {
                    API.sendChatMessageToPlayer(receiver,
                        "You have been demoted to " + member.Group.RankNames[rank] + " by " + sender.CharacterName + ".");
                }
                else
                {
                    API.sendChatMessageToPlayer(receiver,
                        "You have been promoted to " + member.Group.RankNames[rank] + " by " + sender.CharacterName +
                        ".");
                }
                API.sendChatMessageToPlayer(player,
                    "You have changed " + member.CharacterName + "'s rank to " + rank + " (was " + oldRank + ").");
                member.GroupRank = rank;
                member.Save();
            }
            else
            {
                API.sendChatMessageToPlayer(player, Color.White, "You cannot set the rank of a higher ranking member or set someone to a rank above yours.");
            }
        }

        [Command("setdivision")]
        public void setdivision_cmd(Client player, string id, int divId)
        {
            var receiver = PlayerManager.ParseClient(id);
            if (receiver == null)
            {
                API.sendChatMessageToPlayer(player, Color.White, "That player is not connected.");
                return;
            }

            if (divId < 0 || divId > 5)
            {
                API.sendChatMessageToPlayer(player, Color.White, "Valid division IDs are between 0 and 5.");
                return;
            }

            Character character = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(character, 7);

            Character receiverChar = API.getEntityData(receiver.handle, "Character");

            receiverChar.Division = divId;
            receiverChar.DivisionRank = 1;
            receiverChar.Save();

            if (divId != 0)
            {
                API.sendChatMessageToPlayer(receiver, Color.White,
                    character.CharacterName + " has added you to the " + character.Group.Divisions[divId - 1] +
                    " division.");

                API.sendChatMessageToPlayer(player, Color.White, "You have added " + receiverChar.CharacterName + " to the " + character.Group.Divisions[divId - 1] + " division.");
            }
            else
            {
                API.sendChatMessageToPlayer(receiver, Color.White,
                    character.CharacterName + " has removed your position in a division.");

                API.sendChatMessageToPlayer(player, Color.White, "You have removed " + receiverChar.CharacterName + " from a division.");
            }
        }

        [Command("setdivisionrank")]
        public void setdivisionrank_cmd(Client player, string id, int rank)
        {
            var receiver = PlayerManager.ParseClient(id);
            if (receiver == null)
            {
                API.sendChatMessageToPlayer(player, Color.White, "That player is not connected.");
                return;
            }

            if (rank < 1 || rank > 5)
            {
                API.sendChatMessageToPlayer(player, Color.White, "Valid division ranks are between 1 and 5.");
                return;
            }

            Character character = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(character, 7, true, 4);

            Character receiverChar = API.getEntityData(receiver.handle, "Character");

            if (receiverChar.DivisionRank <= character.DivisionRank && character.DivisionRank > rank)
            {

                if (rank > receiverChar.DivisionRank)
                {
                    API.sendChatMessageToPlayer(receiver, Color.White,
                        "You have been promoted in your division to rank " + rank + " by " + character.CharacterName);

                    API.sendChatMessageToPlayer(player, Color.White,
                        "You have promoted " + receiverChar.CharacterName + " in their division to rank " + rank);
                }
                else if (rank <= receiverChar.DivisionRank)
                {
                    API.sendChatMessageToPlayer(receiver, Color.White,
                        "You have been demoted in your division to rank " + rank + " by " + character.CharacterName);

                    API.sendChatMessageToPlayer(player, Color.White,
                        "You have demoted " + receiverChar.CharacterName + " in their division to rank " + rank);
                }

                receiverChar.DivisionRank = rank;
                receiverChar.Save();
            }
            else
            {
                API.sendChatMessageToPlayer(player, Color.White, "You cannot set the rank of a higher ranking member or set someone to a rank above yours.");
            }
        }

        [Command("group", Alias = "g", GreedyArg = true)]
        public void group_cmd(Client player, string message)
        {
            GroupCommandPermCheck(API.getEntityData(player.handle, "Character"), 1);

            Character character = API.getEntityData(player.handle, "Character");
            SendGroupMessage(player, "[G][" + character.GroupRank + "] " + GetRankName(character) + " " + character.CharacterName + " : " + "~w~" + message);
        }

        [Command("gotopoint")]
        public void gotopoint_cmd(Client player, float x, float y, float z, string ipl = "none")
        {
            API.requestIpl(ipl);
            API.setEntityPosition(player.handle, new Vector3(x, y, z));
            API.sendChatMessageToPlayer(player, "TPed");
        }

        [Command("accept")]
        public void accept_cmd(Client player, string option)
        {
            if (option == "groupinvitation")
            {
                Character character = API.getEntityData(player.handle, "Character");
                Character inviteSender = API.getEntityData(player.handle, "GroupInvitation");

                if (inviteSender == null)
                {
                    API.sendChatMessageToPlayer(player, Color.White, "You do not have an active group invitiation.");
                    return;
                }

                character.GroupId = inviteSender.GroupId ;
                character.Group = inviteSender.Group;
                character.GroupRank = 1;
                character.Save();

                API.sendChatMessageToPlayer(player, "You have joined " + inviteSender.Group.Name + ".");

                SendGroupMessage(player,
                    character.CharacterName + " has joined the group. (Invited by: " + inviteSender.CharacterName + ")");
            }
        }

        [Command("quitgroup")]
        public void quitgroup_cmd(Client player)
        {
            Character sender = API.getEntityData(player.handle, "Character");

            if(sender.GroupId == 0)
            {
                API.sendChatMessageToPlayer(player, "You are not in any group.");
                return;
            }

            SendGroupMessage(player, sender.CharacterName + " has left the group. (Quit)");

            sender.GroupId = 0;
            sender.Group = null;
            sender.GroupRank = 0;
            sender.Save();
            API.sendChatMessageToPlayer(player, "You have left " + sender.Group.Name + ".");
        }
        
        [Command("invite")]
        public void invite_cmd(Client player, string id)
        {
            var invited = PlayerManager.ParseClient(id);

            if (invited == null)
            {
                API.sendChatMessageToPlayer(player, Color.White, "That player is not connected.");
                return;
            }

            Character sender = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(sender, 6);

            Character invitedchar = API.getEntityData(invited.handle, "Character");
            API.setEntityData(invited.handle, "GroupInvitation", sender);

            API.sendChatMessageToPlayer(invited, Color.Pm, "You have been invited to " + sender.Group.Name + ". Type /accept groupinvitation.");
            API.sendChatMessageToPlayer(player, "You sent a group invitation to " + invitedchar.CharacterName + ".");
            
        }

        [Command("setrankname", GreedyArg = true)]
        public void setrankname_cmd(Client player, int rankId, string rankname)
        {
            Character character = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(character, 5);
               
            
            if (rankId < 1 || rankId > 10)
            {
                API.sendChatMessageToPlayer(player, Color.White, "Valid ranks are between 1 and 10.");
                return; 
            }

            character.Group.RankNames.RemoveAt(rankId - 1);
            character.Group.RankNames.Insert(rankId - 1, rankname);
            API.sendChatMessageToPlayer(player, "You have set Group ID " + character.Group.Id + " (" + character.Group.Name + ")'s Rank " + rankId + " to " + rankname + ".");
            character.Group.Save();
        }

        [Command("setdivisionname", GreedyArg = true)]
        public void setdivisionname_cmd(Client player, int divId, string divname)
        {
            Character character = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(character, 5, true, 4);

          
            if (divId < 1 || divId > character.Group.Divisions.Count)
            {
                API.sendChatMessageToPlayer(player, Color.White, "Valid division IDs are between 1 and 5.");
                return; 
            }

            character.Group.Divisions.RemoveAt(divId - 1);
            character.Group.Divisions.Insert(divId - 1, divname);
            API.sendChatMessageToPlayer(player, "You have set Group ID " + character.Group.Id + " (" + character.Group.Name + ")'s Division " + divId + " to " + divname + ".");
            character.Group.Save();
        }

        [Command("setdivisionrankname", GreedyArg = true)]
        public void setdivisonrankname_cmd(Client player, int divId, int rankId, string rankName)
        {
            Character character = API.getEntityData(player.handle, "Character");
            GroupCommandPermCheck(character, 5, true, 4);

            if (divId < 1 || divId > 5)
            {
                API.sendChatMessageToPlayer(player, Color.White, "Valid divisions are between 1 and 5.");
                return;
            }

            if (rankId < 1 || rankId > 5)
            {
                API.sendChatMessageToPlayer(player, Color.White, "Valid division ranks are between 1 and 5.");
                return;
            }

            divId--;
            rankId--;


            character.Group.DivisionRanks[divId].RemoveAt(rankId);
            character.Group.DivisionRanks[divId].Insert(rankId, rankName);
            character.Group.Save();

            API.sendChatMessageToPlayer(player, Color.White, "You have changed rank " + (rankId + 1) + "'s name in the " + character.Group.Divisions[divId] + " division to " + rankName);
        }

        [Command("set")]
        public void set_cmd(Client player, string id, string option, int amount)
        {
            var receiver = PlayerManager.ParseClient(id);

            if (receiver == null)
            {
                API.sendNotificationToPlayer(player, "~r~ERROR:~w~ Invalid player entered.");
                return;
            }

            Account account = API.getEntityData(player.handle, "Account");
           
            if (account.AdminLevel == 0)
                return;

            Character character = API.getEntityData(player.handle, "Character");

            switch (option)
            {
                case "group":
                    character.GroupId = amount;
                    character.Group = GetGroupById(amount);
                    character.Save();
                    API.sendChatMessageToPlayer(player, "You have set " + PlayerManager.GetName(receiver) + "[" + id + "]" + "'s group to " + amount + ".");
                    break;
                case "grouprank":
                    if (amount < 1 || amount > 10)
                    {
                        API.sendChatMessageToPlayer(player, Color.Grey, "Valid group ranks are between 1 and 10.");
                        return;
                    }

                    character.GroupRank = amount;
                    character.Save();
                    API.sendChatMessageToPlayer(player, "You have set " + PlayerManager.GetName(receiver) + "[" + id + "]" + "'s group rank to " + amount + ".");
                    break;
                default:
                    API.sendChatMessageToPlayer(player, Color.White, "Valid options are: group, grouprank");
                    break;
            }
        }

        [Command("creategroup", GreedyArg = true)]
        public void creategroup_cmd(Client player, int type, string name)
        {
            Account account = API.getEntityData(player.handle, "Account");
            if (account.AdminLevel < 4)
                return;

            var group = new Group();

            group.Name = name;
            group.Type = type;
            group.Insert();

            API.sendChatMessageToPlayer(player, Color.Grey, "You have created group " + group.Id + " ( " + group.Name + ", Type: " + group.Type + " ). Use /editgroup to edit it.");
        }

        public static Group GetGroupById(int id)
        {
            if (id == 0 || id > Groups.Count)
                return Group.None;

            return (Group)Groups.ToArray().GetValue(id - 1);
        }

        public static void SendGroupMessage(Client player, string message, string color = Color.GroupChat)
        {
            Character sender = API.shared.getEntityData(player.handle, "Character");
            foreach (var c in PlayerManager.Players)
            {
                if(c.GroupId == sender.GroupId)
                {
                    API.shared.sendChatMessageToPlayer(c.Client, color, message);
                }
            }
        }

        public string GetRankName(Character c, bool ignoreDivision = false)
        {
            if (ignoreDivision == false)
            {
                return c.DivisionRank != 0 ? GetDivisonRankName(c) : c.Group.RankNames[c.GroupRank - 1];
            }
            return c.GroupRank == 0 ? "None" : c.Group.RankNames[c.GroupRank - 1];
        }

        public string GetDivisonRankName(Character c)
        {
            return c.DivisionRank == 0 ? "None" : c.Group.DivisionRanks[c.Division - 1][c.DivisionRank - 1];
        }

        public bool GroupCommandPermCheck(Character c, int rank, bool isDivisionCmd = false, int divisionRank = 1)
        {
            if(isDivisionCmd == false)
                if (c.Group != Group.None) return c.GroupRank >= rank;

            if (c.Group != Group.None) return c.DivisionRank >= divisionRank || c.GroupRank >= rank;

            API.sendChatMessageToPlayer(c.Client, "You do not have permission to perform this command.");
            return false;
        }

        public void load_groups()
        {
            Groups = DatabaseManager.GroupTable.Find(Builders<Group>.Filter.Empty).ToList();

            foreach (var g in Groups)
            {
                g.register_markerzones();
            }

            DebugManager.DebugMessage("Loaded " + Groups.Count + " groups from the database.");
        }
    }
}