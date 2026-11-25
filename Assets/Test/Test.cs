
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using UnityEngine;

public class TestResource : IDisposable
{
    public TestResource()
    {
        UnityEngine.Debug.Log("资源创建");
    }

    public void Work()
    {
        UnityEngine.Debug.Log("资源正在使用");
    }

    public void Dispose()
    {
        UnityEngine.Debug.Log("资源释放");
    }
}

public class Test : MonoBehaviour
{

        [ContextMenu("实验：创建多个客户端")]
        public async void MultipleClient()
        {
            for (int i = 0; i < 20; i++)
            {
                using (HttpClient client = new HttpClient(
                    new HttpClientHandler { UseProxy = false }))
                {
                    using (var response = await client.GetAsync(
                        "http://127.0.0.1:8000/health"))
                    {
                        Debug.Log($"请求 {i} 完成");
                    }
                }
            }
        }

    private static readonly HttpClient client =
        new HttpClient(
            new HttpClientHandler { UseProxy = false }
        );

    [ContextMenu("实验：复用客户端")]
    public async void SingleClient()
    {
        for (int i = 0; i < 3; i++)
        {
            using (var response = await client.GetAsync(
                "http://127.0.0.1:8000/health"))
            {
                Debug.Log($"请求 {i} 完成");
            }
        }
    }

    [ContextMenu("3方法")]
    public void StopNotepad()
    {

    }


}
