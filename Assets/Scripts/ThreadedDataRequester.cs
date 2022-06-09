using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

public class ThreadedDataRequester : MonoBehaviour {

    static ThreadedDataRequester instance;
    readonly ConcurrentQueue<ThreadInfo> dataQueue = new();


    void Awake() {
        instance = FindObjectOfType<ThreadedDataRequester>();
    }


    public static void RequestData(Func<object> generateData, Action<object> callback) {
        void threadStart() {
            instance.DataThread(generateData, callback);
        }
        new Thread(threadStart).Start();
    }


    void DataThread(Func<object> generateData, Action<object> callback) {
        object data = generateData();
        lock (dataQueue) {
            dataQueue.Enqueue(new ThreadInfo(callback, data));
        }
    }


    void Update() {
        while(dataQueue.Count > 0) {
            if (!dataQueue.TryDequeue(out ThreadInfo threadInfo)) {
                continue;
            }
            threadInfo.callback(threadInfo.parameter);
        }
    }


    struct ThreadInfo {
        public readonly Action<object> callback;
        public readonly object parameter;

        public ThreadInfo(Action<object> callback, object parameter) {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}
