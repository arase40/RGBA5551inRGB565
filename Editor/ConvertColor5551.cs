using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections;

public class ConvertColor5551 : MonoBehaviour {
	public delegate void Del(Texture2D tex);
	static Del straight = Create5551Texture;
	static Del dither = Create5551Dither;

	[MenuItem("Assets/RGBA5551 Non-Dither")]
	private static void Menu1(){
		if (EditorUtility.DisplayDialog ("override", "元画像は減色して上書きされます。\nよろしいですか？", "はい", "いいえ")) {
			Operate (straight);
		}
	}

	[MenuItem("Assets/RGBA5551 Dither")]
	private static void Menu2(){
		if (EditorUtility.DisplayDialog ("override", "元画像は減色して上書きされます。\nよろしいですか？", "はい", "いいえ")) {
			Operate (dither);
		}
	}

	//Main loop
	private static void Operate(Del callback){
		string targetPath;

		//Operation loop for Select Objects
		foreach (Object target in Selection.objects) {
			targetPath = AssetDatabase.GetAssetPath (target.GetInstanceID ());
			if (Path.GetExtension (targetPath) == ".png") {
				//Preprocess
				TextureImporter textureImporter = AssetImporter.GetAtPath(targetPath) as TextureImporter;
				textureImporter.isReadable = true;
				textureImporter.textureFormat = TextureImporterFormat.ARGB32;
				AssetDatabase.ImportAsset(targetPath);

				//Main-process
				callback((Texture2D) target);

				//Post-process
				TextureRGB16(textureImporter);
				AssetDatabase.ImportAsset(targetPath);
			}
		}
		AssetDatabase.Refresh();
	}

	//Texture Color RGBA5551 in RGB565
	private static void Create5551Texture(Texture2D targetTexture)
	{
		if (targetTexture == null)
			return;
		
		int w = targetTexture.width;
		int h = targetTexture.height;
		Texture2D newTex = new Texture2D (w, h, TextureFormat.ARGB32, false);
		Color32[] pixels = targetTexture.GetPixels32 ();
		Color[] color = new Color[pixels.Length]; //New Pixels
		int a, r, g, b;
		int mask = 248;
		for (int i = 0; i < pixels.Length; i++) {
			a = (pixels[i].a & 128) >> 5;
			r = pixels[i].r & mask;
			g = (pixels[i].g & mask) + a;
			b = pixels[i].b & mask;

			Color dcolor = new Color32 ((byte)r, (byte)g, (byte)b, 255);
			color[i] = dcolor;
		}
		newTex.SetPixels (color);

		WriteTexture (targetTexture, newTex);
		DestroyImmediate(newTex);
	}

	//Texture Color RGBA5551 in RGB565 with Dither
	private static void Create5551Dither(Texture2D targetTexture)
	{
		if (targetTexture == null)
			return;

		int mask = 7;
		int[,] t = new int[4, 3];
		t = new int[,] { {1, 7, 3}, {5, 1, 7}, {7, 3, 5}, {3, 5, 1}}; // threshold

		int w = targetTexture.width;
		int h = targetTexture.height;
		Texture2D newTex = new Texture2D (w, h, TextureFormat.ARGB32, false);
		Color32[] pixels = targetTexture.GetPixels32 ();
		Color[] color = new Color[pixels.Length]; //New Pixels
		int x, y, a;
		for (int i = 0; i < pixels.Length; i++) {
			x = i % w;
			y = i / w;
			
			DitherColor dc = new DitherColor (pixels [i], mask);
			if ((y & 1) == 0) {
				if ((x & 1) == 0) {
					dc.r = dc.lr > t[0, 0] ? dc.ur : dc.dr;
					dc.g = dc.lg > t[0, 1] ? dc.ug : dc.dg;
					dc.b = dc.lb > t[0, 2] ? dc.ub : dc.db;
				} else {
					dc.r = dc.lr > t[1, 0] ? dc.ur : dc.dr;
					dc.g = dc.lg > t[1, 1] ? dc.ug : dc.dg;
					dc.b = dc.lb > t[1, 2] ? dc.ub : dc.db;
				}
			} else {
				if ((x & 1) == 0) {
					dc.r = dc.lr > t[2, 0] ? dc.ur : dc.dr;
					dc.g = dc.lg > t[2, 1] ? dc.ug : dc.dg;
					dc.b = dc.lb > t[2, 2] ? dc.ub : dc.db;
				} else {
					dc.r = dc.lr > t[3, 0] ? dc.ur : dc.dr;
					dc.g = dc.lg > t[3, 1] ? dc.ug : dc.dg;
					dc.b = dc.lb > t[3, 2] ? dc.ub : dc.db;
				}
			}
			a = (pixels[i].a & 128) >> 5;
			dc.g += a;
			
			Color dcolor = new Color32 ((byte)dc.r, (byte)dc.g, (byte)dc.b, 255);
			color[i] = dcolor;
		}
		newTex.SetPixels (color);
		
		WriteTexture (targetTexture, newTex);
		DestroyImmediate(newTex);
	}

	public struct DitherColor{
		public int r,g,b;
		public int lr, lg, lb; //下位ビット
		public int ur, ug, ub; //Up Color
		public int dr, dg, db; //Down Color
		
		public DitherColor(Color32 p, int mask){
			int m = 255 - mask; //max
			int o = mask + 1; // +1
			r = p.r;
			g = p.g;
			b = p.b;
			lr = r&mask;
			lg = g&mask;
			lb = b&mask;
			dr = r&m;
			dg = g&m;
			db = b&m;
			ur = r >= m ? m : dr+o;
			ug = g >= m ? m : dg+o;
			ub = b >= m ? m : db+o;
		}
	}
		
	//Texture2D Asset Overwrite
	private static void WriteTexture(Texture2D srcTex, Texture2D dstTex){
		byte[] texPNG = dstTex.EncodeToPNG();
		if (texPNG != null) {
			string filePath = AssetDatabase.GetAssetPath(srcTex.GetInstanceID());
			File.WriteAllBytes(filePath, texPNG);
			
			AssetDatabase.ImportAsset(filePath);
			AssetDatabase.Refresh();
		}
	}

	//Texture Type Setting
	public static void TextureRGB16(TextureImporter ti)
	{
		ti.textureType = TextureImporterType.Advanced;
		ti.npotScale = TextureImporterNPOTScale.None;
		ti.isReadable = false;
		ti.grayscaleToAlpha = false;
		ti.alphaIsTransparency = false;
		ti.linearTexture = true;
		ti.spriteImportMode = SpriteImportMode.None;
		ti.mipmapEnabled = false;
		ti.wrapMode = TextureWrapMode.Clamp;
		ti.filterMode = FilterMode.Point;
		ti.textureFormat = TextureImporterFormat.RGB16;
	}
}
