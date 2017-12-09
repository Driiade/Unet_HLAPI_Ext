/*Copyright(c) <2017> <Benoit Constantin ( France )>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/

using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingSpawner : MonoBehaviour {

        [SerializeField]
        GameObject prefab;


        GameObject objSpawned;

        void Awake()
        {
            NetworkingSystem.OnStartServer += SpawnObject;
            NetworkingSystem.OnStopServer += DestroyObject;
        }

        void OnDestroy()
        {
                NetworkingSystem.OnStartServer -= SpawnObject;
                NetworkingSystem.OnStopServer -= DestroyObject;
        }

        void SpawnObject(NetworkMessage netMsg)
        {
            NetworkServer.Spawn(objSpawned = Instantiate(prefab, this.transform.position, this.transform.rotation) as GameObject);
        }


        void DestroyObject(NetworkMessage netMsg)
        {
            NetworkServer.Destroy(objSpawned);
        }
    }
}
