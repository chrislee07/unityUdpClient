using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Configuration;

public class NetworkMan : MonoBehaviour
{
    public UdpClient udp;
    public GameObject playerObj;    //player object

    public Dictionary<string, GameObject> currentPlayers;
    public List<string> newPlayers;
    public ListOfPlayers initialPlayers;
    public string myAddy;
    public Player myPlayer = new Player();

    // Start is called before the first frame update
    void Start()
    {
        newPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();
        initialPlayers = new ListOfPlayers();
        
        udp = new UdpClient();
        udp.Connect("18.217.204.205", 12345);
        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
        udp.Send(sendBytes, sendBytes.Length);
        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 4);
        InvokeRepeating("MovePlayer", 1, 2);
    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,             // 0
        GAME_UPDATE,            // 1
        PLAYER_DISCONNECTED,    // 2
        LIST_OF_PLAYERS         // 3
    };
    
    [Serializable]
    public class Message{
        public commands cmd;
    }
    
    [Serializable]
    public class Player{
        [Serializable] 
        public struct receivedColor{
            public float R;
            public float G;
            public float B;
        }
        public string id;
        public Vector3 currPosition;
        public receivedColor color;
    }

    [Serializable]
    public class ListOfPlayers{
        public Player[] players;

        public ListOfPlayers()
        {
            players = new Player[0];
        }
    }

    [Serializable]
    public class GameState{
        public int pktNum;
        public Player[] players;
    }

    public Message latestMessage;
    public GameState latestGameState;
    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        UnityEngine.Debug.Log("Got this: " + returnData);
        
        latestMessage = JsonUtility.FromJson<Message>(returnData);
        try{
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    ListOfPlayers newestPlayer = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    foreach(Player player in newestPlayer.players)
                    {
                        newPlayers.Add(player.id);
                        myAddy = player.id;
                    }
                    break;
                case commands.GAME_UPDATE:
                    latestGameState = JsonUtility.FromJson<GameState>(returnData);
                    UnityEngine.Debug.Log("latestGameState" + returnData);
                    break;
                case commands.PLAYER_DISCONNECTED:
                    DestroyPlayers(returnData);
                    break;
                case commands.LIST_OF_PLAYERS:
                    initialPlayers = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    break;
                default:
                    UnityEngine.Debug.Log("Error");
                    break;
            }
        }
        catch (Exception e){
            UnityEngine.Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }

    void SpawnPlayers()
    {
        if (newPlayers.Count > 0)
        {
            foreach (string playerID in newPlayers)
            {
                currentPlayers.Add(playerID,Instantiate(playerObj, new Vector3(1, 0, 0), Quaternion.identity));
                currentPlayers[playerID].name = playerID;
                myPlayer.currPosition = currentPlayers[playerID].transform.position;
                UnityEngine.Debug.Log("Connected to server, Position: " + currentPlayers[playerID].transform.position);
            }
            newPlayers.Clear();
        }
        if (initialPlayers.players.Length > 0)
        {
            //UnityEngine.Debug.Log(initialPlayers.players);
            foreach(Player player in initialPlayers.players)
            {
                if (player.id == myAddy)
                    continue;
                currentPlayers.Add(player.id, Instantiate(playerObj, new Vector3(0, 1, 0), Quaternion.identity));
                currentPlayers[player.id].name = player.id;
            }
            initialPlayers.players = new Player[0];
        }
    }

    void UpdatePlayers()
    {
        if(latestGameState.players.Length > 0)
        {
            foreach (NetworkMan.Player player in latestGameState.players)
            {
                string playerID = player.id;
                myPlayer.currPosition = currentPlayers[myAddy].GetComponent<Transform>().position;
                //currentPlayers[playerID].GetComponent<Transform>().position = new Vector3(player.currPosition.x, player.currPosition.y, player.currPosition.z);
                UnityEngine.Debug.Log(playerID + " position : " + currentPlayers[playerID].transform.position);
            }
            latestGameState.players = new Player[0];
        }

    }

    void DestroyPlayers(string disPlayer)
    {
        UnityEngine.Debug.Log("Disconnected Player: " + disPlayer);

    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void MovePlayer()
    {
        string playerPos = JsonUtility.ToJson(myPlayer);
        Byte[] sendBytes = Encoding.ASCII.GetBytes(playerPos);
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        SpawnPlayers();
        UpdatePlayers();
        //DestroyPlayers();
        //MovePlayer();
    }
}
