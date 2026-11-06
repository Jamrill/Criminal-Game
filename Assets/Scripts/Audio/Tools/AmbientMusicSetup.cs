#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using JuegoCriminal.Audio;
using UnityEditor;
using UnityEngine;

public static class AmbientMusicSetup
{
    const string Folder="Assets/Audio/Music",Track=Folder+"/Neon_Quiet_Hours.wav";
    const int Rate=44100;
    public static void Apply()
    {
        Directory.CreateDirectory(Folder+"/Resources");
        if(!File.Exists(Track))Compose();
        AssetDatabase.Refresh();
        var importer=(AudioImporter)AssetImporter.GetAtPath(Track);
        var sample=importer.defaultSampleSettings;sample.loadType=AudioClipLoadType.Streaming;sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.75f;
        importer.defaultSampleSettings=sample;importer.forceToMono=false;importer.SaveAndReimport();
        string path=Folder+"/Resources/AmbientMusic.asset";
        var settings=AssetDatabase.LoadAssetAtPath<AmbientMusicSettings>(path);
        if(!settings)
        {
            settings=ScriptableObject.CreateInstance<AmbientMusicSettings>();
            settings.tracks=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>(Track)};AssetDatabase.CreateAsset(settings,path);
        }
        AssetDatabase.SaveAssets();AuditFolders();
        Debug.Log("AMBIENT_MUSIC_SETUP_OK clip="+Track+" length="+settings.tracks[0].length);
    }
    static void Compose()
    {
        // Original 16-bar cue in A minor, 78 BPM: warm electric keys, airy
        // ninth chords and sparse half-time electronic percussion. No samples.
        double beat=60.0/78,bar=beat*4,duration=16*bar+4;
        int length=(int)(duration*Rate);var left=new float[length];var right=new float[length];
        var random=new System.Random(76031);
        void Note(double at,double seconds,int midi,double gain,double pan,int voice)
        {
            double frequency=440*Math.Pow(2,(midi-69)/12.0);
            int start=(int)(at*Rate),samples=(int)(seconds*Rate);
            double l=Math.Sqrt((1-pan)*.5),r=Math.Sqrt((1+pan)*.5);
            for(int i=0;i<samples&&start+i<length;i++)
            {
                double t=(double)i/Rate,u=t/seconds,phase=2*Math.PI*frequency*t,v,env;
                if(voice==0) // FM-tine electric piano, soft attack and round tail.
                {
                    env=(1-Math.Exp(-t*180))*Math.Exp(-t*1.7)*Math.Min(1,(seconds-t)/.22);
                    v=(Math.Sin(phase+.45*Math.Exp(-t*4)*Math.Sin(phase*2))+.16*Math.Sin(phase*2)*Math.Exp(-t*4))*env;
                }
                else if(voice==1) // A slow, gently detuned pad under the keys.
                {
                    env=Math.Pow(Math.Sin(Math.PI*u),1.25);
                    v=(Math.Sin(phase)+Math.Sin(phase*1.0015)+.16*Math.Sin(phase*2))*.45*env;
                }
                else // Restrained bass; no aggressive sub impact.
                {
                    env=(1-Math.Exp(-t*45))*Math.Exp(-t*2.2)*Math.Min(1,(seconds-t)/.18);
                    v=(Math.Sin(phase)+.18*Math.Sin(phase*2))*env;
                }
                left[start+i]+=(float)(v*gain*l);right[start+i]+=(float)(v*gain*r);
            }
        }
        void Percussion(double at,bool kick,double gain)
        {
            int start=(int)(at*Rate),n=(int)(Rate*(kick?.18:.07));double prev=0;
            for(int i=0;i<n&&start+i<length;i++)
            {
                double t=(double)i/Rate,noise=random.NextDouble()*2-1;
                double sound=kick?Math.Sin(2*Math.PI*(52*t+1.3*(1-Math.Exp(-t*60))))*Math.Exp(-t*33):(noise-prev)*Math.Exp(-t*85);
                sound*=Math.Min(1,t/.002)*Math.Min(1,(n-i)/(Rate*.012));prev=noise;
                left[start+i]+=(float)(sound*gain*.7);right[start+i]+=(float)(sound*gain*.7);
            }
        }
        int[][] chords={new[]{57,60,64,71},new[]{53,57,60,67},new[]{55,59,62,69},new[]{52,55,59,62}};
        int[][] melody={new[]{76,71,74,72},new[]{72,69,67,71},new[]{74,71,69,67},new[]{71,67,64,69}};
        double[] offsets={.5,1.75,2.5,3.25};
        for(int b=0;b<16;b++)
        {
            int c=(b/2)%4;double at=b*bar;
            if(b%2==0)
            {
                foreach(int note in chords[c])Note(at,bar*2+.4,note,.047,(note%5-2)*.23,1);
                for(int j=0;j<4;j++)Note(at+j*.025,3.8,chords[c][j],.085,(j-1.5)*.18,0);
            }
            if(b>=2 && b<15)
            {
                int count=b%2==0?3:2;
                for(int j=0;j<count;j++)
                {
                    int note=melody[c][(j+(b>=8?1:0))%4];
                    Note(at+offsets[j+(b%2)]*beat,2.6,note,.105+(j==0?.018:0),(j-1)*.22,0);
                }
                Note(at+.035,2.1,chords[c][0]-24,.11,0,2);
            }
            if(b>=4 && b<14)
            {
                Percussion(at,true,.075);if(b%2==1)Percussion(at+2.5*beat,true,.04);
                for(int h=0;h<4;h++)Percussion(at+(h+.55)*beat,false,.009);
            }
        }
        // Sparse stereo cross-delay and diffuse reflections, written offline.
        var dryL=(float[])left.Clone();var dryR=(float[])right.Clone();
        double[] taps={.113,.179,beat*.75,beat*1.5};double[] levels={.10,.085,.16,.065};
        for(int k=0;k<taps.Length;k++)
        {
            int offset=(int)(taps[k]*Rate);
            for(int i=offset;i<length;i++){left[i]+=dryR[i-offset]*(float)levels[k];right[i]+=dryL[i-offset]*(float)levels[k];}
        }
        double peak=0,sum=0;
        for(int i=0;i<length;i++)
        {
            double time=(double)i/Rate,fade=Math.Min(1,time/1.5)*Math.Min(1,(duration-time)/4);
            left[i]*=(float)fade;right[i]*=(float)fade;peak=Math.Max(peak,Math.Max(Math.Abs(left[i]),Math.Abs(right[i])));
        }
        double scale=.72/Math.Max(.001,peak);
        using(var writer=new BinaryWriter(File.Create(Track)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+length*4);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(Rate);writer.Write(Rate*4);writer.Write((short)4);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(length*4);
            for(int i=0;i<length;i++)
            {double l=left[i]*scale,r=right[i]*scale;sum+=l*l+r*r;writer.Write((short)(l*short.MaxValue));writer.Write((short)(r*short.MaxValue));}
        }
        File.WriteAllText("Documentation/AmbientMusicRender.txt","Original composition: Neon Quiet Hours\n78 BPM; 16 bars; stereo PCM16 44100 Hz\nSeconds="+duration+"\nPeak=0.72\nRMS="+Math.Sqrt(sum/(length*2))+"\n");
    }
    public static void AuditFolders()
    {
        var roots=new[]{"Assets/Scenes/01_MainMenu.unity","Assets/Scenes/10_World_City.unity","Assets/ConstructionCatalog.asset"};
        var dependencies=AssetDatabase.GetDependencies(roots,true).ToHashSet();
        var paths=AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/Meshes","Assets/Construction"}).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p=>p).ToArray();
        var report=new System.Collections.Generic.List<string>{"Roots: "+string.Join(", ",roots),"Mesh assets="+paths.Length,"Referenced from scene/catalog roots="+paths.Count(dependencies.Contains)};
        foreach(string path in paths)report.Add((dependencies.Contains(path)?"USED ":"NOT_IN_ROOT_DEPENDENCIES ")+path);
        File.WriteAllLines("Documentation/ConstructionMeshUsage.txt",report);
        Debug.Log("CONSTRUCTION_MESH_USAGE_AUDIT_OK total="+paths.Length+" used="+paths.Count(dependencies.Contains));
    }
}
#endif
