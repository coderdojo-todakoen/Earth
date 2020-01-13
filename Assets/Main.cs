using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Main : MonoBehaviour
{
    void Awake()
    {
        Calc();
    }

    /**
     * タイル座標を経度へ変換します
     * https://www.trail-note.net/tech/coordinate/
     * を参考にしました
     */
    private static double Longitude(int x, int z)
    {
        return 180 * ((x * 256) / Math.Pow(2, z + 7) - 1);
    }

    /**
     * tanhの逆関数です
     */
    private static double Atanh(double x)
    {
        return Math.Log((1 + x) / (1 - x)) / 2;
    }

    /**
     * タイル座標を緯度へ変換します
     * https://www.trail-note.net/tech/coordinate/
     * を参考にしました
     */
    private static double Latitude(int y, int z)
    {
        const double L = 85.05112878;
        return (180 / Math.PI) * Math.Asin(
                Math.Tanh(-Math.PI * y * 256 / Math.Pow(2, z + 7) + Atanh(Math.Sin(Math.PI * L / 180))));

    }

    /**
     * 角度をラジアンへ変換します
     */
    private static double Radian(double angle)
    {
        return angle * Math.PI / 180;
    }

    void Calc()
    {
        // 頂点の分割数
        int k = 6;
        int kmax = (int)Math.Pow(2, k);

        // メッシュの分割数=OSMのズームレベル
        // k(頂点の分割数)以下の値です
        int l = 3;
        int lmax = (int)Math.Pow(2, l);

        // 頂点座標
        List<Vector3> vertices = new List<Vector3>();

        // UV座標
        List<Vector2> uvs = new List<Vector2>();

        // 頂点座標を求めます
        for (int y = 0; y != lmax; ++y)
        {
            for (int x = 0; x != lmax; ++x)
            {
                // サブメッシュ毎に頂点座標を求めます
                int mmax = kmax / lmax;
                for (int yy = 0; yy <= mmax; ++yy)
                {
                    for (int xx = 0; xx <= mmax; ++xx)
                    {
                        // タイル座標を緯度・経度へ変換します
                        double lon = Longitude(x * mmax + xx, k);
                        double lat = Latitude(y * mmax + yy, k);

                        // 緯度・経度を(x, y, z)座標へ変換します
                        float vx = (float)(Math.Cos(Radian(lat)) * Math.Cos(Radian(lon)));
                        float vy = (float)Math.Sin(Radian(lat));
                        float vz = (float)(Math.Cos(Radian(lat)) * Math.Sin(Radian(lon)));

                        // 頂点座標を追加します
                        vertices.Add(new Vector3(vx, vy, vz));

                        // 頂点が対応するUV座標も求めます
                        uvs.Add(new Vector2((float)xx / mmax, (float)(mmax - yy) / mmax));
                    }
                }
            }
        }

        // メッシュを作成します
        Mesh mesh = new Mesh();

        // 頂点座標をメッシュへ格納します
        mesh.vertices = vertices.ToArray();

        // サブメッシュの数を設定します
        mesh.subMeshCount = lmax * lmax;
        int submesh = 0;

        List<Material> materials = new List<Material>();

        for (int y = 0; y != lmax; ++y)
        {
            for (int x = 0; x != lmax; ++x)
            {
                // サブメッシュ毎にメッシュを構成するポリゴンの座標を求め
                // 頂点座標のインデックスを格納します
                List<int> triangles = new List<int>();

                int mmax = kmax / lmax;
                int nmax = (mmax + 1) * (mmax + 1);
                for (int yy = 0; yy != mmax; ++yy)
                {
                    for (int xx = 0; xx != mmax; ++xx)
                    {
                        // 左上の三角形
                        triangles.Add((y * lmax + x) * nmax + yy * (mmax + 1) + xx);
                        triangles.Add((y * lmax + x) * nmax + yy * (mmax + 1) + xx + 1);
                        triangles.Add((y * lmax + x) * nmax + (yy + 1) * (mmax + 1) + xx);

                        // 右下の三角形
                        triangles.Add((y * lmax + x) * nmax + yy * (mmax + 1) + xx + 1);
                        triangles.Add((y * lmax + x) * nmax + (yy + 1) * (mmax + 1) + xx + 1);
                        triangles.Add((y * lmax + x) * nmax + (yy + 1) * (mmax + 1) + xx);
                    }
                }

                // サブメッシュを構成するポリゴンを追加します
                mesh.SetTriangles(triangles, submesh++);

                Material material = new Material(GetComponent<MeshRenderer>().material.shader);
                materials.Add(material);
            }
        }

        mesh.uv = uvs.ToArray();

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        GetComponent<MeshFilter>().mesh = mesh;

        GetComponent<MeshRenderer>().materials = materials.ToArray();

        StartCoroutine(GetTexture(l));
    }

    // OSMのタイルを取得してテクスチャとして設定します
    IEnumerator GetTexture(int zl)
    {
        int max = (int)Math.Pow(2, zl);

        Material[] materials = GetComponent<MeshRenderer>().materials;
        for (int y = 0; y < max; ++y)
        {
            for (int x = 0; x < max; ++x)
            {
                // ズームレベル、タイル座標を指定して地図画像を取得します
                String url = String.Format("https://tile.openstreetmap.org/{0}/{1}/{2}.png", zl, x, y);

                UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                yield return www.SendWebRequest();

                // 取得した画像をテクスチャとして設定します
                materials[y * max + x].mainTexture = DownloadHandlerTexture.GetContent(www);
            }
        }
    }
    void Update()
    {
        // 自転しているように見せるため、少し回転します
        transform.Rotate(0, -0.2f, 0);
    }

    public void OnShowLicense()
    {
        // クレジットがクリックされたら、以下のリンクを表示します
        Application.OpenURL("https://www.openstreetmap.org/copyright");
    }

}
