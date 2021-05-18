using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MultipleSpouseDialog
{
    public class Chatting
    {
        public static List<string> chattingSpouses = new List<string>();
        public static int lastChatTime = 0;
        public static SoundEffect kissEffect = null;

        private static bool Eligible(NPC npc1, NPC npc2)
        {
            float distance = Vector2.Distance(npc1.position, npc2.position);
            double roll = ModEntry.myRand.NextDouble();
            bool lucky = roll < ModEntry.config.SpouseChatChance;

            return
                npc1.getSpouse() != null && npc2.getSpouse() != null
                    && npc1.getSpouse().Name == npc2.getSpouse().Name
                    && distance >= ModEntry.config.MinDistanceToChat
                    && distance < ModEntry.config.MaxDistanceToChat
                    && !chattingSpouses.Contains(npc1.Name)
                    && !chattingSpouses.Contains(npc2.Name)
                    && !npc1.isSleeping
                    && !npc2.isSleeping
                    && lastChatTime > ModEntry.config.MinSpouseChatInterval
                    && lucky
                    && (!ModEntry.config.PreventRelativesFromChatting || !Misc.AreSpousesRelated(npc1.Name, npc2.Name));
        }

        private static bool Eligible(NPC npc1, Farmer npc2)
        {
            float distance = Vector2.Distance(npc1.position, npc2.position);
            double roll = ModEntry.myRand.NextDouble();
            bool lucky = roll < ModEntry.config.SpouseChatChance;
            bool already = chattingSpouses.Contains(npc1.Name) || chattingSpouses.Contains(npc2.Name);
            bool in_range = distance >= ModEntry.config.MinDistanceToChat && distance < ModEntry.config.MaxDistanceToChat;

            return
                npc1.getSpouse() != null && npc2.getSpouse() != null
                    && in_range
                    && !already
                    && !npc1.isSleeping
                    && lastChatTime > ModEntry.config.MinSpouseChatInterval
                    && lucky
                    ;
        }

        public static void TrySpousesChat()
        {
            GameLocation location = Game1.currentLocation;

            if (location == null || !ReferenceEquals(location.GetType(), typeof(FarmHouse)))
                return;

            lastChatTime++;

            if (location == null || location.characters == null)
                return;

            int n_spouses = location.characters.ToList().Count;
            if (ModEntry.myRand.Next(n_spouses) == 0)
            {
                if (ModEntry.config.ChatWithPlayer) TryPlayerChat();
                TryNPCsChat();

            } else
            {
                TryNPCsChat();
                if (ModEntry.config.ChatWithPlayer) TryPlayerChat();
            }
        }

        private static void TryPlayerChat()
        {
            GameLocation location = Game1.currentLocation;
            Farmer owner = (location as FarmHouse).owner;

            List<NPC> list = location.characters.ToList();

            Misc.ShuffleList(ref list);

            if (lastChatTime >= ModEntry.config.MinSpouseChatInterval)
                chattingSpouses.Clear();

            foreach (NPC npc1 in list)
            {
                if (!owner.friendshipData.ContainsKey(npc1.Name)) continue;
                if (owner.getFriendshipHeartLevelForNPC(npc1.Name) >= ModEntry.config.MinHeartsForChat) continue;

                //Monitor.Log($"NPC/Player {npc1.Name} {owner.Name} eligible: {Eligible(npc1, owner)}", LogLevel.Debug);
                if (Eligible(npc1, owner))
                {
                    chattingSpouses.Add(npc1.Name);
                    chattingSpouses.Add(owner.Name);
                    if (ModEntry.config.ExtraDebugOutput) ModEntry.PMonitor.Log($"Chatting: {npc1.Name} (faces {npc1.facingDirection}) {owner.Name} (faces {owner.facingDirection})", LogLevel.Debug);
                    lastChatTime = 0;
                    int npc1face = npc1.facingDirection;
                    int npc2face = owner.facingDirection;

                    PerformChat(npc1, owner);

                    //DelayedAction action = new DelayedAction(5000);
                    var t = Task.Run(async delegate
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));

                        npc1.FacingDirection = npc1face;
                        owner.FacingDirection = npc2face;
                        return;
                    });
                }
            }
        }

        private static void TryNPCsChat()
        {
            GameLocation location = Game1.currentLocation;
            Farmer owner = (location as FarmHouse).owner;

            List<NPC> list = location.characters.ToList();

            Misc.ShuffleList(ref list);

            foreach (NPC npc1 in list)
            {
                if (!owner.friendshipData.ContainsKey(npc1.Name)) continue;
                if (owner.getFriendshipHeartLevelForNPC(npc1.Name) >= ModEntry.config.MinHeartsForChat) continue;

                foreach (NPC npc2 in list)
                {
                    if (npc1.Name == npc2.Name) continue;

                    if (!owner.friendshipData.ContainsKey(npc2.Name)) continue;
                    if (owner.getFriendshipHeartLevelForNPC(npc2.Name) >= ModEntry.config.MinHeartsForChat) continue;

                    // Monitor.Log($"NPCs {npc1.Name} {npc2.Name} dist {distance} roll {roll} vs {ModEntry.config.SpouseChatChance}: {lucky}", LogLevel.Debug);
                    if (Eligible(npc1, npc2))
                    {
                        chattingSpouses.Add(npc1.Name);
                        chattingSpouses.Add(npc2.Name);
                        if (ModEntry.config.ExtraDebugOutput) ModEntry.PMonitor.Log($"Chatting: {npc1.Name} (faces {npc1.facingDirection}) {npc2.Name} (faces {npc2.facingDirection})", LogLevel.Debug);
                        lastChatTime = 0;
                        int npc1face = npc1.facingDirection;
                        int npc2face = npc2.facingDirection;

                        PerformChat(npc1, npc2);

                        //DelayedAction action = new DelayedAction(5000);
                        var t = Task.Run(async delegate
                        {
                            await Task.Delay(TimeSpan.FromSeconds(4));

                            npc1.FacingDirection = npc1face;
                            npc2.FacingDirection = npc2face;
                            return;
                        });
                    }
                }
            }
        }

        private static void PerformChat(NPC npc1, Character npc2)
        {
            Vector2 midpoint = new Vector2((npc1.position.X + npc2.position.X) / 2, (npc1.position.Y + npc2.position.Y) / 2);
            NPC caller;
            Character responder;
            bool caller_face_left = false;

            if (npc1.position.X < midpoint.X)
            {
                caller = npc1;
                responder = npc2;
            } else if (npc2 is Farmer) {
                caller = npc1;
                responder = npc2;
                caller_face_left = true;
            }
            else
            {
                caller = npc2 as NPC;
                responder = npc1;
            }

            if (caller is null)
            {
                ModEntry.PMonitor.Log($"PerformChat: {caller.Name} (left) is farmer, cannot chat", LogLevel.Error);
                return;
            }

            ModEntry.PMonitor.Log($"PerformChat {caller.Name} (partner: {responder.Name})  Farmer? {(npc2 is Farmer)}", LogLevel.Trace);

            ConfigDialog d = Dialog.RandomDialog(caller.Name, responder.Name, npc2 is Farmer);
            string call = d.Call.Replace("@", responder.displayName);
            string response = d.Response.Replace("@", caller.displayName);

            if (ModEntry.config.ExtraDebugOutput) ModEntry.PMonitor.Log($"PerformChat {caller.Name}: {call} / {responder.Name}: {response}", LogLevel.Debug);

            int delay = 2000;

            caller.faceDirection(caller_face_left ? 3 : 1);
            caller.showTextAboveHead($"{call}", -1, 2, delay, 0);
            caller.Sprite.UpdateSourceRect();

            if (responder is NPC)
            {
                NPC rn = responder as NPC;
                rn.faceDirection(3);
                rn.showTextAboveHead($"{response}", -1, 2, delay, delay);
                rn.Sprite.UpdateSourceRect();

                caller.movementPause = delay * 2;
                rn.movementPause = delay * 2;
            }
            else
            {
                caller.movementPause = delay;
                ((Farmer)responder).doEmote(20);
            }
        }
    }
}
