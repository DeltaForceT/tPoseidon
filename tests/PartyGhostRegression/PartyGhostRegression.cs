using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.Packets;
sealed class Clock:TimeProvider {public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
static class Program{
static int checks;static int failures;static void Check(bool yes,string text){checks++;if(!yes){failures++;Console.WriteLine("FAIL "+text);}else Console.WriteLine("PASS "+text);}
static PartyOutbox Login(PartyService p,ulong id)=>p.Login(id,"P"+id,1,60);
static PartyService Setup(Clock c){var p=new PartyService(Encoding.UTF8,new PartyRules(1,TimeSpan.FromMinutes(10)),c);Login(p,1);Login(p,2);p.Make(1,"TEST","P2");p.AcceptInvite(2);Check(p.PartyMembers(1).Count==2,"create/accept party");return p;}
static void Main(){Run();PartyLifecycleChecks.Run();PartyCustomDivisionChecks.Run();}
static bool HasSnapshot(PartyOutbox o,ulong id)=>o.Any(m=>m.To==id&&m.Opcode==PartyFrames.JoinState);
static void Run(){
foreach(var scenario in new[]{"normal logout","forced-close after disconnect callback","network loss after disconnect callback"}){var c=new Clock();var p=Setup(c);p.Logout(2);var returned=Login(p,2);Check(p.PartyMembers(2).Count==2,scenario+": membership retained");Check(HasSnapshot(returned,2),scenario+": returning client receives full JoinState");p.Leave(2);Check(p.PartyMembers(2).Count==0,scenario+": leave works after return");}
{var c=new Clock();var p=Setup(c);Check(HasSnapshot(Login(p,2),2),"relogin before old disconnect: resynchronize existing membership");Check(p.PartyMembers(1).Count==2,"duplicate login does not duplicate member");}
{var c=new Clock();var p=Setup(c);p.Logout(1);Check(p.Rename(2,"HANDOVER").Any(m=>m.Opcode==PartyFrames.RenameDone),"leader logout: online successor can manage party");Check(HasSnapshot(Login(p,1),1),"former leader receives snapshot on return");}
foreach(var first in new ulong[]{1,2}){var c=new Clock();var p=Setup(c);p.Logout(1);p.Logout(2);Check(p.PartyCount==1,"all clients offline: party retained during grace");var a=Login(p,first);var b=Login(p,first==1?2UL:1UL);Check(HasSnapshot(a,first)&&HasSnapshot(b,first==1?2UL:1UL),"all clients reconnect: both get snapshots, first="+first);Check(p.PartyMembers(1).Count==2&&p.PartyMembers(2).Count==2,"all clients reconnect: consistent membership, first="+first);Check(p.Rename(first,"RETURNED").Any(m=>m.Opcode==PartyFrames.RenameDone),"first returning member can lead, first="+first);}
{var c=new Clock();var p=Setup(c);p.Logout(1);p.Logout(2);c.Now+=TimeSpan.FromMinutes(11);p.ExpireOfflineMembers();Check(p.PartyCount==0,"offline expiry removes abandoned party");Check(!HasSnapshot(Login(p,1),1),"expired party not restored");Login(p,2);p.Make(1,"NEW","P2");p.AcceptInvite(2);Check(p.PartyMembers(1).Count==2,"party can be created again after expiry");}
Console.WriteLine($"{checks} assertions, {failures} failures. Service-level only; no real socket, process or power-off test.");Environment.ExitCode=failures==0?0:1;
}}
