#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using JuegoCriminal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UIButtonAudioSetup
{
    const string Folder="Assets/Audio/UI";
    static readonly string[] Scenes={"Assets/Scenes/01_MainMenu.unity","Assets/Scenes/10_World_City.unity"};
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first");
        Directory.CreateDirectory(Folder+"/Resources");
        Generate("UI_Appear_Whoosh",.28,0);Generate("UI_Hover_Cylinder",.145,1);Generate("UI_Click_SoftShot",.105,2);
        AssetDatabase.Refresh();
        foreach(string path in Directory.GetFiles(Folder,"*.wav"))
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            var sample=importer.defaultSampleSettings;sample.loadType=AudioClipLoadType.DecompressOnLoad;sample.compressionFormat=AudioCompressionFormat.PCM;
            importer.defaultSampleSettings=sample;importer.forceToMono=true;importer.SaveAndReimport();
        }
        const string settingsPath=Folder+"/Resources/UIButtonSounds.asset";
        var settings=AssetDatabase.LoadAssetAtPath<UIButtonSoundSettings>(settingsPath);
        if(!settings){settings=ScriptableObject.CreateInstance<UIButtonSoundSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
        settings.appear=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/UI_Appear_Whoosh.wav");
        settings.hover=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/UI_Hover_Cylinder.wav");
        settings.click=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/UI_Click_SoftShot.wav");EditorUtility.SetDirty(settings);
        string backup="Documentation/UIButtonAudioBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        void Backup(string path){string dest=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(path,dest);}
        int added=0;
        var prefabs=AssetDatabase.GetDependencies(Scenes,true).Concat(new[]{"Assets/Prefabs/UI/Button_Tablet.prefab"})
            .Where(p=>p.StartsWith("Assets/")&&p.EndsWith(".prefab")).Distinct()
            .OrderBy(p=>AssetDatabase.GetDependencies(p,true).Length).ToArray();
        foreach(string path in prefabs)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var buttons=root.GetComponentsInChildren<Button>(true).Where(b=>!b.GetComponent<UIButtonAudio>()).ToArray();
                if(buttons.Length==0)continue;
                Backup(path);foreach(var button in buttons){UIButtonAudio.Ensure(button);added++;}
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);
            var buttons=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Where(b=>!b.GetComponent<UIButtonAudio>()).ToArray();
            if(buttons.Length==0)continue;
            Backup(path);foreach(var button in buttons){UIButtonAudio.Ensure(button);added++;}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();Debug.Log("UI_BUTTON_AUDIO_SETUP_OK added="+added+" backup="+backup);
        Validate();SlotPencilValidation.Run();
    }
    // Original procedural foley. Seeded noise, filtered air, resonant metal
    // and a short low-frequency body; no recordings or third-party samples.
    static void Generate(string name,double duration,int kind)
    {
        string path=Folder+"/"+name+".wav";if(File.Exists(path))return;
        const int rate=48000;int count=(int)(duration*rate);var values=new double[count];var random=new System.Random(7341+kind);
        double low=0,mid=0,peak=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/rate,u=t/duration,n=random.NextDouble()*2-1;
            low+=.075*(n-low);mid+=.38*(n-mid);double band=mid-low,v;
            if(kind==0)
            {
                double env=Math.Pow(Math.Sin(Math.PI*u),1.7);
                v=env*(.8*low+.45*band)+.035*Math.Sin(2*Math.PI*(160*t-100*t*t))*env;
            }
            else if(kind==1)
            {
                v=0;
                foreach(double start in new[]{0,.028,.064})
                {
                    double q=t-start;if(q<0)continue;
                    v+=Math.Exp(-q*135)*band*.6;
                    v+=.1*Math.Exp(-q*72)*(Math.Sin(2*Math.PI*1550*q)+.4*Math.Sin(2*Math.PI*2390*q));
                }
                v+=.1*Math.Exp(-t*45)*Math.Sin(2*Math.PI*350*t);
            }
            else
            {
                v=.7*Math.Exp(-t*180)*band+.32*Math.Exp(-t*65)*Math.Sin(2*Math.PI*(180*t-430*t*t));
                if(t>.022){double q=t-.022;v+=.14*Math.Exp(-q*155)*(band+Math.Sin(2*Math.PI*1250*q)*.3);}
            }
            // Short fades remove sample-boundary clicks (apart from designed transients).
            v*=Math.Min(1,t/.001)*Math.Min(1,(duration-t)/.012);values[i]=v;peak=Math.Max(peak,Math.Abs(v));
        }
        using var writer=new BinaryWriter(File.Create(path));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        foreach(double sample in values)writer.Write((short)(sample/Math.Max(.001,peak)*.8*short.MaxValue));
    }
    public static void Validate()
    {
        var s=AssetDatabase.LoadAssetAtPath<UIButtonSoundSettings>(Folder+"/Resources/UIButtonSounds.asset");
        if(!s || !s.appear || !s.hover || !s.click)throw new Exception("Missing UI audio settings/clips");
        foreach(var clip in new[]{s.appear,s.hover,s.click})
        {
            clip.LoadAudioData();var samples=new float[clip.samples*clip.channels];if(!clip.GetData(samples,0))throw new Exception("Cannot read "+clip.name);
            float peak=samples.Max(v=>Mathf.Abs(v));if(peak<.1f||peak>.95f||clip.length>.4f)throw new Exception("Invalid level/duration: "+clip.name);
            Debug.Log("UI_AUDIO_PASS "+clip.name+" duration="+clip.length+" peak="+peak);
        }
        foreach(string scenePath in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(scenePath);
            foreach(var button in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)))
                if(!button.GetComponent<UIButtonAudio>())throw new Exception("Button without audio: "+button.name);
        }
        Debug.Log("UI_BUTTON_AUDIO_VALIDATION_OK");
    }
}
#endif
