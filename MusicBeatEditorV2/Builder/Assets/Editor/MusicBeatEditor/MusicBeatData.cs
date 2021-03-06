﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JSON;
using System;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Excel;
using UnityEditor;

public enum EnumLineType
{
    Tick,
    UpNote,
    DownNote,
    Floor,
    Env,
    Obstacle,
    Event,
}




//[System.Serializable]
public struct EBeatLineData : IComparable
{
    public string EventBeatNumber;
    //	public string EventTime;
    public string EventInfo;
    public JSONNode Info
    {
        get
        {
            return JSONNode.Parse(EventInfo);
        }
    }
    public EBeatLineData(string beatNumber, string info = "{}")
    {
        EventBeatNumber = beatNumber;
        //		EventTime = beatNumber;
        EventInfo = info;
    }
    public int CompareTo(object obj)
    {
        if (obj is EBeatLineData)
        {
            int tmpBeatNumber = int.Parse(EventBeatNumber);

            return tmpBeatNumber.CompareTo(int.Parse(((EBeatLineData)obj).EventBeatNumber));
        }

        return 1;
    }
}



[System.Serializable]
public struct EBeatLine
{
    public EBeatLine(string name, EnumLineType eventtype)
    {
        LineName = name;
        LineDataID = new Dictionary<string, EBeatLineData>();
        EventType = eventtype;
    }
    public string LineName;
    public EnumLineType EventType;
    public Dictionary<string, EBeatLineData> LineDataID;

}

[System.Serializable]
public class MusicBpmInfo
{
    public float bpm = 60;
    public int startBeat = 0;
    public float beatTime = 0;
}

public class MusicBeatData
{
    public float bpm
    {
        get
        {
            if (_beatChartJson != null)
            {
                return _beatChartJson.GetBpm();
            }
            return 0;
        }
        set
        {
            if (_beatChartJson != null)
            {
                if (_beatChartJson.GetBpm() != value)
                {
                    _beatChartJson.SetBpm(value);
                }
            }
        }
    }

    public float GetBpmNew(int beat)
    {
        return _beatChartJson.GetBpmNew(beat);
    }

    public List<MusicBpmInfo> bpmTbl
    {
        get
        {
            return _beatChartJson.bpmTbl;
        }
    }

    public void ApplyBpmChange()
    {
        if(_beatChartJson.bpmTbl.Count <= 1)
        {
            Debug.Log("无需变速");
            return;
        }

        bool bRet = true;
        for(int index = 0; index < _beatChartJson.bpmTbl.Count; index++)
        {
            MusicBpmInfo currInfo = _beatChartJson.bpmTbl[index];
            //if(currInfo.endBeat <= currInfo.startBeat)
            //{
            //    bRet = false;
            //    break;
            //}

            if (index >= _beatChartJson.bpmTbl.Count - 1)
            {
                break;
            }


            MusicBpmInfo nextInfo = _beatChartJson.bpmTbl[index + 1];
            if(nextInfo.startBeat <= currInfo.startBeat)
            {
                bRet = false;
                break;
            }
        }

        if(bRet == true)
        {
            _beatChartJson.BuildBeatListForBpmChange();
        }
        else
        {
            Debug.Log("bpm数据有误！！");
        }
    }

    public void AddBpmInfo(int bpm , int start)
    {
        _beatChartJson.AddBpmInfo(bpm, start);
    }

    public void RemoveBpmInfo(int index)
    {
        _beatChartJson.RemoveBpmInfo(index);
    }



    public MusicBpmInfo GetBpmInfo(int beat)
    {
        return _beatChartJson.GetBpmInof(beat);
    }


    public float MaxTerrain { get { return _beatChartJson.MaxTerrainLen; } }

    public float GetMaxTerrain(int beat)
    {
        return _beatChartJson.GetMaxTerrainLen(beat);
    }

    public float chartLength
    {
        get
        {
            if (_beatChartJson != null)
            {
                return _beatChartJson.GetChartLength();
            }
            return 0;
        }
        set
        {
            if (_beatChartJson.GetChartLength() != value)
            {
                _beatChartJson.SetChartLength(value);
            }
        }
    }

    public float musicOffset
    {
        get
        {
            if (_beatChartJson != null)
            {
                return _beatChartJson.musicOffset;
            }
            return 0;
        }
        set
        {
            if (_beatChartJson.musicOffset != value)
            {
                _beatChartJson.musicOffset = value;
            }
        }
    }

    public float musicLength
    {
        get
        {
            if (mbAudioSource != null)
                return mbAudioSource.length;

            return 1;
        }
    }

    public string musicName
    {
        get
        {
            if (_beatChartJson != null)
            {
                return _dictMusicIdToName[_beatChartJson.musicId];
            }
            return "";
        }
    }

    public int sceneID {
        set
        {
            if (_beatChartJson.sceneId != value)
            {
                _beatChartJson.sceneId = value;
            }
        }

        get
        {
            if (_beatChartJson != null)
            {
                return _beatChartJson.sceneId;
            }
            return 0;
        }
    }
    public List<EBeatLine> Lines = new List<EBeatLine>();
    private MusicBeatUndo _musicBeatUndo;
    public int _comboPerBeatInHold = 1;
    public MusicBeatData()
    {
        Lines.Add(new EBeatLine("节拍", EnumLineType.Tick));
        Lines.Add(new EBeatLine("上轨音符", EnumLineType.UpNote));
        Lines.Add(new EBeatLine("下轨音符", EnumLineType.DownNote));
        Lines.Add(new EBeatLine("障碍", EnumLineType.Obstacle));
        Lines.Add(new EBeatLine("地面", EnumLineType.Floor));
        Lines.Add(new EBeatLine("环境", EnumLineType.Env));

        _musicBeatUndo = new MusicBeatUndo();
    }

    public void Reset()
    {
    }

    private MusicBeatAudioSource _musicBeatAudioSource;
    public MusicBeatAudioSource mbAudioSource
    {
        get
        {
            if (_musicBeatAudioSource == null)
            {
                _musicBeatAudioSource = new MusicBeatAudioSource();
            }
            return _musicBeatAudioSource;
        }
    }

    public void MusicForward()
    {
        mbAudioSource.time += oneBeatLength;
    }

    public void MusicBack()
    {
        mbAudioSource.time -= oneBeatLength;
    }

    public void ChangeMusic()
    {
        _beatChartJson.musicId = System.Convert.ToInt32(_musicIdsArray[_musicIdsArrayIndex]);
        LoadMusicClip(_beatChartJson.musicId);
    }

    public string[] _musicIdsArray;
    public int _musicIdsArrayIndex = 0;
    Dictionary<string, int> _dictMusicIdToIndex;
    Dictionary<int, string> _dictMusicIdToName;
    public void LoadMusicNames()
    {
        _dictMusicIdToIndex = new Dictionary<string, int>();
        _dictMusicIdToName = new Dictionary<int, string>();

        List<string> list = new List<string>();

        MusicDataProtypes mdProtypes = MusicDataExporter.ReadExcel();
        foreach (MusicDataProtype protype in mdProtypes.musicDatas)
        {
            if (protype._id >= 1 && protype._id <= 10000)
            {
                list.Add(protype._id.ToString());
            }

            if (_dictMusicIdToName.ContainsKey(protype._id))
            {
                UnityEngine.Debug.LogError("MusicData有重复数据:" + protype._id);
                continue;
            }
            _dictMusicIdToName.Add(protype._id, protype._prefabName);
        }

        for (int i = 0; i < list.Count; ++i)
        {
            _dictMusicIdToIndex.Add(list[i], i);
        }
        _musicIdsArray = list.ToArray();
    }

    public void LoadMusicClip(int musicId)
    {
        string clipname = GetMusicClipName(musicId);
        mbAudioSource.LoadClip(clipname);
    }

    public string GetMusicClipName(int musicId)
    {
        string ret = null;
        if (!_dictMusicIdToName.TryGetValue(musicId, out ret))
        {
            UnityEngine.Debug.LogError("没有对应的音频. musicId=" + musicId);
        }
        return ret;
    }

    private MusicBeatChartJson _beatChartJson;
    public void CreateChart()
    {
        _beatChartJson = MusicBeatChartJson.Create();
    }

    public void LoadChart(string path)
    {
        _musicBeatUndo.Clear();
        _musicIdsArrayIndex = 0;

        FileInfo fInfo = new FileInfo(path);
        if (fInfo.Exists)
        {
            Reset();
            string content = File.ReadAllText(path);
            LoadChartFromJson(content);
        }
    }

    void LoadChartFromJson(string content)
    {
        _beatChartJson = JsonUtility.FromJson<MusicBeatChartJson>(content);
        if (_beatChartJson.musicId == 0)
        {
            _musicIdsArrayIndex = 0;
            int id = System.Convert.ToInt32(_musicIdsArray[_musicIdsArrayIndex]);
            _beatChartJson.musicId = id;
        }
        else
        {
            _musicIdsArrayIndex = _dictMusicIdToIndex[_beatChartJson.musicId.ToString()];
        }
        LoadMusicClip(_beatChartJson.musicId);
    }

    public void Save(string filepath)
    {
        //List<MusicBeatFloorJson> newFloorTbl = new List<MusicBeatFloorJson>();
        //newFloorTbl.Add(_beatChartJson.floors[0]);
         
        //for (int floorindex = 0; floorindex < _beatChartJson.floors.Count;floorindex++)
        //{
        //    MusicBeatFloorJson currFloor = _beatChartJson.floors[floorindex];
        //    MusicBeatFloorJson targetFloor = newFloorTbl[newFloorTbl.Count - 1];

        //    if (currFloor.beatIndex == targetFloor.beatIndex && currFloor.noteIndex == targetFloor.noteIndex)
        //    {
        //        //newFloorTbl.Add(currFloor);
        //        continue;
        //    }
        //    newFloorTbl.Add(currFloor);
        //}

        //_beatChartJson.floors = newFloorTbl;

        string str = JsonUtility.ToJson(_beatChartJson);
        File.WriteAllText(filepath, str);
    }

    public MusicBeatJson GetMusicBeatJson(int beat)
    {
        if (!CheckBeatRangeInvalid(beat, beat))
            return null;

        return _beatChartJson.beats[beat];
    }

    public bool SplitBeat(int from, int to, int split)
    {
        if (!CheckBeatRangeInvalid(from, to))
            return false;

        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        int lastFloorHeight = 0;
        if (from > 0)
        {
            int lastsplit = _beatChartJson.beats[from - 1].split;
            lastFloorHeight = _beatChartJson.GetFloor(from - 1, lastsplit - 1).heightlevel;
        }
        for (int i = from; i <= to; ++i)
        {
            _beatChartJson.SetBeatSplit(i, split, lastFloorHeight);
        }

        return true;
    }

    public void CopyNoteId(int targetID, int currBeat, int currNoteIndex, int targetBeat, EnumLineType noteType)
    {
        if(currBeat >= targetBeat)
        {
            return;
        }

        for(int index = currBeat; index <= targetBeat; index++)
        {
            List<MusicBeatNoteJson> notes = GetNote(index, noteType);
            foreach(var item in notes)
            {
                if (currBeat == item.beatIndex && item.noteIndex <= currNoteIndex)
                {
                    continue;
                }


                item.noteID = targetID;
            }
        }
    }

    public void CopyIdentity(int targetIdentity, int currBeat, int currNoteIndex, int targetBeat, EnumLineType noteType)
    {
        if (currBeat >= targetBeat)
        {
            return;
        }

        for (int index = currBeat; index <= targetBeat; index++)
        {
            List<MusicBeatNoteJson> notes = GetNote(index, noteType);
            foreach (var item in notes)
            {
                if(currBeat == item.beatIndex && item.noteIndex <= currNoteIndex)
                {
                    continue;
                }

                item.noteIdentity = targetIdentity;
            }
        }
    }

    public void RemoveNote(int beat, int note, EnumLineType noteType)
    {
        if (beat * note < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveNote(beat, note, noteType);
    }

    public void RemoveNote(int beat, EnumLineType noteType)
    {
        if (beat < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveNote(beat, noteType);
    }

    public MusicBeatNoteJson SetNote(int beat, int note, int type, int typeEx, int yeah, EnumLineType noteType)
    {
        MusicBeatNoteJson res = null;
        if (beat < 0 || note < 0)
        {
            return null;
        }

        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        MusicBeatNoteJson m = _beatChartJson.GetNote(beat, note, noteType);
        if (m != null)
        {
            res = _beatChartJson.SetNote(beat, note, type, typeEx, noteType);
        }
        else
        {
            res = _beatChartJson.AddNote(beat, note, type, typeEx, noteType);
        }

        _beatChartJson.SetNoteYeah(beat, note, yeah, noteType);

        return res;
    }

    public MusicBeatNoteJson GetNote(int beat, int note, EnumLineType noteType)
    {
        return _beatChartJson.GetNote(beat, note, noteType);
    }

    public List<MusicBeatNoteJson> GetNote(int beat, EnumLineType noteType)
    {
        return _beatChartJson.GetNote(beat, noteType);
    }

    public int GetNoteYeah(int beat, int note, EnumLineType noteType)
    {
        MusicBeatNoteJson json = GetNote(beat, note, noteType);
        if (json != null)
            return json.yeah;
        return 0;
    }

    public MusicBeatFloorJson GetFloor(int beat, int note)
    {
        return _beatChartJson.GetFloor(beat, note);
    }

    public void AddFloor(int beat, int note, int t)
    {
        if (beat < 0 || note < 0)
        {
            return;
        }

        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.AddFloor(beat, note, t);
    }

    public void RemoveFloor(int beat, int note)
    {
        if (beat * note < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveFloor(beat, note);
    }

    public void RemoveFloor(int beat, int note, int t)
    {
        if (beat * note < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveFloor(beat, note, t);
    }

    public MusicBeatObstacleJosn GetObstacle(int beat, int note)
    {
        return _beatChartJson.GetObstacle(beat, note);
    }

    public void AddObstacle(int beat, int note, int id)
    {
        if (beat < 0 || note < 0)
        {
            return;
        }

        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.AddObstacle(beat, note, id);
    }

    public void RemoveObstacle(int beat, int note)
    {
        if (beat * note < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveObstacle(beat, note);
    }


    public MusicBeatEnvJson GetEnv(int beat, int note)
    {
        return _beatChartJson.GetEnv(beat, note);
    }

    public void AddEnv(int beat, int note, int t)
    {
        if (beat < 0 || note < 0)
        {
            return;
        }

        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.AddEnv(beat, note, t);
    }

    public void RemoveEnv(int beat, int note)
    {
        if (beat * note < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveEnv(beat, note);
    }

    public void RemoveEnv(int beat, int note, int t)
    {
        if (beat * note < 0)
        {
            return;
        }
        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));
        _beatChartJson.RemoveEnv(beat, note, t);
    }

    public bool CheckBeatRangeInvalid(int s, int e)
    {
        if (s < 0 || e >= _beatChartJson.beatCount || s > e)
            return false;

        return true;
    }


    public float Proportion
    {
        get
        {
            return chartTime / chartLength;
        }
    }

    public float chartTime
    {
        get
        {
            if (mbAudioSource == null || _beatChartJson == null)
                return 0f;

            return Mathf.Max(0f, mbAudioSource.time - musicOffset);
        }
    }

    public float oneBeatLength
    {
        get
        {
            if (_beatChartJson == null)
                return 1;

            return _beatChartJson.beatTime;
        }
    }

    public int GetBeatByAudioTime(float time)
    {
        int beat = 0;
        float totalTime = 0;
        
        for(int i = 0; i < _beatChartJson.beats.Count; i++)
        {
            MusicBeatJson currBeat = _beatChartJson.beats[i];
            if(i == _beatChartJson.beats.Count - 1)
            {
                beat = i;
                break;
            }

            float startTime = totalTime;
            float endTime = totalTime + currBeat.noteTime * currBeat.split;

            if(time >= startTime && time < endTime)
            {
                beat = i;
                break;
            }
            totalTime += currBeat.noteTime * currBeat.split;
           
        }

        return beat;
    }

    

    public float getOneBeatLength(int beat)
    {
        if (_beatChartJson == null)
            return 1;

        return _beatChartJson.GetBeatTime(beat);
    }

    public float getAudioTimeByBeat(int beat)
    {
        float totalTime = 0;

        for (int i = 0; i < _beatChartJson.beats.Count; i++)
        {
            MusicBeatJson currBeat = _beatChartJson.beats[i];
            if (i == _beatChartJson.beats.Count - 1)
            {
             
                break;
            }

            if(i == beat)
            {
                break;
            }

            totalTime += currBeat.noteTime * currBeat.split;
        }

        return totalTime;
    }

    public int beatCount
    {
        get
        {
            if (_beatChartJson == null)
                return 0;

            return _beatChartJson.beatCount;
        }
    }

    public float floatBeatCount
    {
        get
        {
            if (_beatChartJson == null)
                return 0;

            return _beatChartJson.fBeatCount;
        }
    }

    public void Undo()
    {
        string content = _musicBeatUndo.Pop();
        if (string.IsNullOrEmpty(content))
            return;

        LoadChartFromJson(content);
    }

    public int HistoryCount()
    {
        return _musicBeatUndo.Count;
    }

    public bool Statistics(ref string ret)
    {
        return _beatChartJson.Check(_comboPerBeatInHold, ref ret);
    }

    public void CopyTriggersData(string content)
    {
        if (_beatChartJson == null)
            return;

        _musicBeatUndo.Push(JsonUtility.ToJson(_beatChartJson));

        MusicBeatChartJson json = JsonUtility.FromJson<MusicBeatChartJson>(content);
        if (json.floors != null && json.floors.Count > 0)
        {
            _beatChartJson.floors = json.floors;
        }

        if (json.envs != null && json.envs.Count > 0)
        {
            _beatChartJson.envs = json.envs;
        }
    }

}

//该枚举对应BeatItem表
public enum EnumBeatItemType
{
    eNone = -1,
    eNormaNote = 0,
    eReverseNote,
    eLaunchNote,
    eHoldNote,
    eBossComboNote,
    eJumpUpNote,
    eJumpDownNote,
    eBossNote,
    eBossReverseNote,

    eUp,  //上
    eDown,
    eLeftNote, //左音符
    eRightNote,
    eUpHold,
    eDownHold,
    eLeftHold,
    eRightHold,
    eLeftPlatform, //左踏板
    eRightPlatform,
    eRightAndUp, //右上
    eUpAndUp,
    eLeftAndUp,
    eRightCross, //右穿过,
    eLeftCross,
    eRightAndUpHold, //右上hold
    eLeftAndUpHold
}

//编辑器用
public enum EnumBeatItemTypeEx
{
    eNone = 0,
    eHoldBegin = 1,
    eHoldEnd = 2
}

[Serializable]
public class MusicBeatChartJson
{
    public int musicId;
    public float musicOffset = 0f;
    public int sceneId = 1;

    [SerializeField] private float chartLength = 60f;
    [SerializeField] private float bpm = 60f;

    public List<MusicBpmInfo> bpmTbl = new List<MusicBpmInfo>();

    public float fBeatCount = 60f;
    public int beatCount = 60;
    public float beatTime = 1f;

    public List<MusicBeatJson> beats = new List<MusicBeatJson>();
    public List<MusicBeatNoteJson> upNotes = new List<MusicBeatNoteJson>();
    public List<MusicBeatNoteJson> downNotes = new List<MusicBeatNoteJson>();
    public List<MusicBeatObstacleJosn> obstacleNotes = new List<MusicBeatObstacleJosn>();
    public List<MusicBeatFloorJson> floors = new List<MusicBeatFloorJson>();
    public List<MusicBeatEnvJson> envs = new List<MusicBeatEnvJson>();

    private float dropspeed = 80;
    private float dropdis = 200;
    private float standerSplite = 8;
    private float maxTerrainLen = 0;
    public float MaxTerrainLen
    {
        get {

   
            int split = beats[0].split;
            float perbeatTime = 60.0f / bpm;
            float perElementTime = perbeatTime / split;
            float dropTime = dropdis / dropspeed;
            int maxTerrainlenElem = (int)(dropTime / perElementTime);
            maxTerrainLen = maxTerrainlenElem / split + (float)((maxTerrainlenElem % split) * 0.1);

            maxTerrainLen = dropTime / perbeatTime;
            maxTerrainLen = (float)Math.Round(maxTerrainLen, 2, MidpointRounding.AwayFromZero);

            return maxTerrainLen;
        }
    }

    public float GetMaxTerrainLen(int beat)
    {
        float currBpm = GetBpmNew(beat);

        int split = beats[0].split;
        float perbeatTime = 60.0f / currBpm;
        float perElementTime = perbeatTime / split;
        float dropTime = dropdis / dropspeed;
        int maxTerrainlenElem = (int)(dropTime / perElementTime);
        maxTerrainLen = maxTerrainlenElem / split + (float)((maxTerrainlenElem % split) * 0.1);

        maxTerrainLen = dropTime / perbeatTime;
        maxTerrainLen = (float)Math.Round(maxTerrainLen, 2, MidpointRounding.AwayFromZero);

        return maxTerrainLen;
    }

    public void SetBpm(float b)
    { 
        bpm = b;
        bpm = Mathf.Max(bpm, 1f);

        Calc();
    }

    public void SetBpm(float b, int startBeat, int endBeat)
    {
        bpm = b;
        bpm = Mathf.Max(bpm, 1f);

        //bpmTbl.Add(bpm);

        Calc();
    }

    public float GetBpm()
    { return bpm; }

    public float GetBpmNew(int beat)
    {
        float value = bpm;
        if(bpmTbl.Count > 0)
        {
            value = GetBpmInof(beat).bpm;
        }

        return value;
    }


    public void SetChartLength(float l)
    {
        chartLength = l;
        chartLength = Mathf.Max(chartLength, 1f);
        Calc();
    }
    public float GetChartLength()
    { return chartLength; }

    void Calc()
    {
        fBeatCount = bpm * chartLength / 60f;
        beatCount = Mathf.CeilToInt(fBeatCount);
        beatTime = 60.0f / bpm;

        BuildBeatList();
        BuildFloorList();
    }

    public void AddBpmInfo(int bpm, int start)
    {
        MusicBpmInfo newInfo = new MusicBpmInfo();
        newInfo.bpm = bpm;
        newInfo.startBeat = start;
 
        bpmTbl.Add(newInfo);
    }

    public void RemoveBpmInfo(int index)
    {
        bpmTbl.RemoveAt(index);
    }

    public MusicBpmInfo GetBpmInof(int beat)
    {
        MusicBpmInfo info = null;

        for(int i = 0; i < bpmTbl.Count; i++)
        {
            MusicBpmInfo currInfo = bpmTbl[i];
            if(i == bpmTbl.Count - 1)
            {
                info = currInfo;
                break;
            }
            MusicBpmInfo nextInfo = bpmTbl[i + 1];
            if(currInfo.startBeat <= beat && nextInfo.startBeat > beat)
            {
                info = currInfo;
                break;
            }

        }

        return info;
    }



    void BuildFloorList()
    {
        if(floors == null)
        {
            floors = new List<MusicBeatFloorJson>();
        }

        for(int beatindex = 0; beatindex < beatCount; beatindex++)
        {
            MusicBeatJson currBeat = beats[beatindex];
            for(int noteindex = 0; noteindex < currBeat.split; noteindex++)
            {
                RemoveFloor(beatindex, noteindex);

                MusicBeatFloorJson m = new MusicBeatFloorJson();
                m.beatIndex = beatindex;
                m.noteIndex = noteindex;
                //m.AddTrigger(trigger);

                AddFloor(m);
            }
        }
    }

    void BuildBeatList()
    {
        if (beats == null)
            beats = new List<MusicBeatJson>();

        if (beats.Count > beatCount)
        {
            for (int i = beatCount; i < beats.Count; ++i)
            {
                RemoveNote(i, EnumLineType.DownNote);
                RemoveNote(i, EnumLineType.UpNote);
                RemoveNote(i, EnumLineType.Obstacle);
                RemoveNote(i, EnumLineType.Env);
                RemoveFloor(i);
            }
            beats.RemoveRange(beatCount, (beats.Count - beatCount));
        }
        else
        {
            for (int i = beats.Count; i < beatCount; ++i)
            {
                MusicBeatJson mbj = new MusicBeatJson();
                beats.Add(mbj);
                SetBeatSplit(i, 8, 0);
            }
        }

        for (int i = 0; i < beats.Count; ++i)
        {
            beats[i].noteTime = beatTime / beats[i].split;
        }

    }


    public void BuildBeatListForBpmChange()
    {
        fBeatCount = bpm * chartLength / 60f;
        beatCount = Mathf.CeilToInt(fBeatCount);
        beatTime = 60.0f / bpm;


        beats = new List<MusicBeatJson>();

        //fBeatCount = bpm * chartLength / 60f;
        //beatCount = Mathf.CeilToInt(fBeatCount);
        //beatTime = 60.0f / bpm;
        float currChartLen = 0;
        for (int index = 0; index < bpmTbl.Count; index++)
        {
            MusicBpmInfo currInfo = bpmTbl[index];
            float currBeatTime = 60.0f / currInfo.bpm;
            currInfo.beatTime = currBeatTime;

            int startBeat = currInfo.startBeat;
            int endBeat = 0;
            if(index < bpmTbl.Count - 1)
            {
                endBeat = bpmTbl[index + 1].startBeat; 
            }
            else
            {
                endBeat = startBeat + Mathf.CeilToInt(currInfo.bpm * (chartLength - currChartLen) / 60);
            }

            for (int i = startBeat; i < endBeat; ++i)
            {
                MusicBeatJson mbj = new MusicBeatJson();
                mbj.noteTime = currBeatTime / mbj.split;
                beats.Add(mbj);
                SetBeatSplit(i, 8, 0);
              
                currChartLen += currBeatTime;
            }
        }

        beatCount = beats.Count;
        fBeatCount = beatCount;
        BuildFloorList();

    }

    public float GetBeatTime(int beat)
    {
        float currBeatTime = 0;
        if (bpmTbl.Count <= 0)
        {
            currBeatTime = beatTime;
        }
        else
        {
            currBeatTime = GetBpmInof(beat).beatTime;
        }

        return currBeatTime;
    }

    public void SetBeatSplit(int i, int split, int lastHeightLevle)
    {

        float currBeatTime = GetBeatTime(i);

        beats[i].split = split;
        beats[i].noteTime = currBeatTime / split;

        //重新分隔, 删除这个节拍的音符
        RemoveNote(i, EnumLineType.UpNote);
        RemoveNote(i, EnumLineType.DownNote);
        RemoveObstacle(i);
        RemoveEnv(i);

        RemoveFloor(i);
        for (int index = 0; index < beats[i].split; index++)
        {
            MusicBeatFloorJson floor = AddFloor(i, index, 0);
            floor.heightlevel = lastHeightLevle;
        }
        
    }

    public void AddNote(MusicBeatNoteJson note, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);

        notes.Add(note);
        notes.Sort();
    }

    public MusicBeatNoteJson AddNote(int beat, int note, int type, int typeEx, EnumLineType noteType)
    {
        MusicBeatNoteJson m = new MusicBeatNoteJson();
        m.beatIndex = beat;
        m.noteIndex = note;
        m.type = type;
        m.exType = typeEx;

        AddNote(m, noteType);

        return m;
    }

    public void RemoveNote(int beat, int note, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);

        for (int i = 0; i < notes.Count; ++i)
        {
            if (notes[i].beatIndex == beat && notes[i].noteIndex == note)
            {
                notes.RemoveAt(i);
                break;
            }
        }
    }

    public void RemoveNote(int beat, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);


        for (int i = 0; i < notes.Count; ++i)
        {
            if (notes[i].beatIndex == beat)
            {
                notes.RemoveAt(i--);
            }
        }
    }

    public MusicBeatNoteJson SetNote(int beat, int note, int type, int typeEx, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);
        MusicBeatNoteJson res = null;

        for (int i = 0; i < notes.Count; ++i)
        {
            if (notes[i].beatIndex == beat && notes[i].noteIndex == note)
            {
                notes[i].type = type;
                notes[i].exType = typeEx;
                res = notes[i];
                break;
            }
        }

        return res;
    }

    public void SetNoteYeah(int beat, int note, int yeah, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);


        for (int i = 0; i < upNotes.Count; ++i)
        {
            if (notes[i].beatIndex == beat && notes[i].noteIndex == note)
            {
                notes[i].yeah = yeah;
                break;
            }
        }
    }

    public List<MusicBeatNoteJson> GetNotes(EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = null;
        if (noteType == EnumLineType.UpNote)
        {
            notes = upNotes;
        }
        else if (noteType == EnumLineType.DownNote)
        {
            notes = downNotes;
        }

        return notes;
    }

    public MusicBeatNoteJson GetNote(int beat, int note, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);

        for (int i = 0; i < notes.Count; ++i)
        {
            if (notes[i].beatIndex == beat && notes[i].noteIndex == note)
            {
                return notes[i];
            }
        }
        return null;
    }

    public List<MusicBeatNoteJson> GetNote(int beat, EnumLineType noteType)
    {
        List<MusicBeatNoteJson> notes = GetNotes(noteType);

        List<MusicBeatNoteJson> list = new List<MusicBeatNoteJson>();
        for (int i = 0; i < notes.Count; ++i)
        {
            if (notes[i].beatIndex == beat)
            {
                list.Add(notes[i]);
            }
        }
        return list;
    }

    public float GetNoteRunTime(int beatIdx, int noteIdx, EnumLineType noteType)
    {
        MusicBeatNoteJson note = GetNote(beatIdx, noteIdx, noteType);
        return GetNoteRunTime(note);
    }
    public float GetNoteRunTime(MusicBeatNoteJson note)
    {
        float ret = 0f;
        if (note != null)
        {
            float noteTime = beats[note.beatIndex].noteTime;
            float currBeatTime = GetBeatTime(note.beatIndex);

            ret = note.beatIndex * currBeatTime + note.noteIndex * noteTime;
        }
        return ret;
    }

    public void AddEnv(int beat, int note, int trigger)
    {
        MusicBeatEnvJson m = GetEnv(beat, note);
        if (m != null)
        {
            m.AddTrigger(trigger);
        }
        else
        {
            m = new MusicBeatEnvJson();
            m.beatIndex = beat;
            m.noteIndex = note;
            m.AddTrigger(trigger);

            AddEnv(m);
        }
    }

    public void AddEnv(MusicBeatEnvJson m)
    {
        envs.Add(m);
        envs.Sort();
    }

    public void RemoveEnv(int beat, int note)
    {
        for (int i = 0; i < envs.Count; ++i)
        {
            if (envs[i].beatIndex == beat && envs[i].noteIndex == note)
            {
                envs.RemoveAt(i);
                break;
            }
        }
    }

    public void RemoveEnv(int beat)
    {
        for (int i = 0; i < envs.Count; ++i)
        {
            if (envs[i].beatIndex == beat)
            {
                envs.RemoveAt(i--);
            }
        }
    }

    public void RemoveEnv(int beat, int note, int trigger)
    {
        MusicBeatEnvJson m = GetEnv(beat, note);
        if (m != null)
        {
            m.RemoveTrigger(trigger);

            if (m.IsEmpty())
            {
                RemoveEnv(beat, note);
            }
        }
    }

    public MusicBeatEnvJson GetEnv(int beat, int note)
    {
        for (int i = 0; i < envs.Count; ++i)
        {
            if (envs[i].beatIndex == beat && envs[i].noteIndex == note)
            {
                return envs[i];
            }
        }
        return null;
    }


    public MusicBeatFloorJson AddFloor(int beat, int note, int trigger)
    {
        MusicBeatFloorJson m = GetFloor(beat, note);
        if (m != null)
        {
            m.AddTrigger(trigger);
        }
        else
        {
            m = new MusicBeatFloorJson();
            m.beatIndex = beat;
            m.noteIndex = note;
            m.AddTrigger(trigger);

            AddFloor(m);
        }

        return m;
    }

    public void AddFloor(MusicBeatFloorJson m)
    {
        floors.Add(m);
        floors.Sort();
    }

    public void RemoveFloor(int beat, int note)
    {
        for (int i = 0; i < floors.Count; ++i)
        {
            if (floors[i].beatIndex == beat && floors[i].noteIndex == note)
            {
                floors.RemoveAt(i);
                break;
            }
        }
    }

    public void RemoveFloor(int beat)
    {
        for (int i = 0; i < floors.Count; ++i)
        {
            if (floors[i].beatIndex == beat)
            {
                floors.RemoveAt(i--);
            }
        }
    }

    public void RemoveFloor(int beat, int note, int trigger)
    {
        MusicBeatFloorJson m = GetFloor(beat, note);
        if (m != null)
        {
            m.RemoveTrigger(trigger);

            if (m.IsEmpty())
            {
                RemoveFloor(beat, note);
            }
        }
    }

    public MusicBeatFloorJson GetFloor(int beat, int note)
    {
        for (int i = 0; i < floors.Count; ++i)
        {
            if (floors[i].beatIndex == beat && floors[i].noteIndex == note)
            {
                return floors[i];
            }
        }
        return null;
    }

    public void AddObstacle(int beat, int note, int id)
    {
        MusicBeatObstacleJosn m = GetObstacle(beat, note);
        if (m != null)
        {
            m.obstacleID = id;
        }
        else
        {
            m = new MusicBeatObstacleJosn();
            m.beatIndex = beat;
            m.noteIndex = note;
            m.obstacleID = id;

            AddObstacle(m);
        }
    }

    public void AddObstacle(MusicBeatObstacleJosn m)
    {
        obstacleNotes.Add(m);
        obstacleNotes.Sort();
    }

    public void RemoveObstacle(int beat, int note)
    {
        for (int i = 0; i < obstacleNotes.Count; ++i)
        {
            if (obstacleNotes[i].beatIndex == beat && obstacleNotes[i].noteIndex == note)
            {
                obstacleNotes.RemoveAt(i);
                break;
            }
        }
    }

    public void RemoveObstacle(int beat)
    {
        for (int i = 0; i < obstacleNotes.Count; ++i)
        {
            if (obstacleNotes[i].beatIndex == beat)
            {
                obstacleNotes.RemoveAt(i--);
            }
        }
    }

    public MusicBeatObstacleJosn GetObstacle(int beat, int note)
    {
        for (int i = 0; i < obstacleNotes.Count; ++i)
        {
            if (obstacleNotes[i].beatIndex == beat && obstacleNotes[i].noteIndex == note)
            {
                return obstacleNotes[i];
            }
        }

        return null;
    }

    public static MusicBeatChartJson Create()
    {
        MusicBeatChartJson ret = new MusicBeatChartJson();

        ret.musicId = 0;
        ret.chartLength = 60f;
        ret.musicOffset = 0f;
        ret.bpm = 60f;
        ret.Calc();
        
        return ret;
    }

    //长按的Combo计算, 首尾算2个, 中间如下
    //comboPerBeatInHold: hold的情况下, 一个拍子共有几个combo
    //在首Combo后的第一个combo, 计算如下
    public int GetHoldComboCout(MusicBeatNoteJson noteStart, MusicBeatNoteJson noteEnd, int comboPerBeatInHold, ref List<float> listComboTime)
    {
        if (noteStart.exType != (int) EnumBeatItemTypeEx.eHoldBegin)
        {
            EnumBeatItemTypeEx e = (EnumBeatItemTypeEx) (noteStart.exType);
            MyDebug.LogError(string.Format("GetHoldComboCout Hold开始类型错误 [{0}-{1}-{2}]", noteStart.beatIndex, noteStart.noteIndex, e));
            return 0;
        }

        if (noteEnd.exType != (int) EnumBeatItemTypeEx.eHoldEnd)
        {
            EnumBeatItemTypeEx e = (EnumBeatItemTypeEx)(noteEnd.exType);
            MyDebug.LogError(string.Format("GetHoldComboCout Hold结尾类型错误 [{0}-{1}-{2}]", noteEnd.beatIndex, noteEnd.noteIndex, e));
            return 0;
        }

        int count = listComboTime.Count;
        float currBeatTime = GetBeatTime(noteStart.beatIndex);

        float comboInterval = currBeatTime / comboPerBeatInHold;
        float noteStartTime = 0f; //首Combo的时间
        float noteEndTime = 0f; //尾Combo的时间
        {
            //首combo, noteStart
            float noteTime = beats[noteStart.beatIndex].noteTime;
            noteStartTime = noteStart.beatIndex * currBeatTime + noteStart.noteIndex * noteTime;
            listComboTime.Add(noteStartTime);

            //尾combo, noteEnd, 提前计算, 先不添加到列表
            noteTime = beats[noteEnd.beatIndex].noteTime;
            noteEndTime = noteEnd.beatIndex * currBeatTime + noteEnd.noteIndex * noteTime;
        }

        {
            //首尾之间的
            // 1.noteStart所在拍子的combo点
            {
                float time1 = noteStart.beatIndex * currBeatTime; //该拍子的起始时间
                float time2 = (noteStart.beatIndex + 1) * currBeatTime; //下一拍的起始时间
                for (int i = 0; ; i++)
                {
                    float t = time1 + (comboInterval * i);

                    if (t >= time2) //超出了当前拍
                    {
                        break;
                    }

                    if (t >= noteEndTime) //超出了结尾
                    {
                        break;
                    }

                    if (t > noteStartTime)
                    {
                        listComboTime.Add(t);
                    }
                }
            }

            // 2.中间拍(完整拍子)的combo点
            int beatDelta = noteEnd.beatIndex - noteStart.beatIndex - 1;//首尾间隔了几拍
            if (beatDelta > 0)
            {
                for (int i = 0; i < beatDelta; ++i)
                {
                    int beatIdx = (noteStart.beatIndex + 1) + i;
                    float time1 = beatIdx * currBeatTime; //该拍子的起始时间
                    for (int j = 0; j < comboPerBeatInHold; ++j)
                    {
                        float t = time1 + comboInterval * j;
                        listComboTime.Add(t);
                    }
                }
            }

            // 3.noteEnd所在拍子的combo点, 不和noteStart在同一个拍子上
            if(noteEnd.beatIndex > noteStart.beatIndex)
            {
                float time1 = noteEnd.beatIndex * currBeatTime; //该拍子的起始时间

                for (int i = 0; ; i++)
                {
                    float t = time1 + (comboInterval * i);
                    
                    if (t >= noteEndTime) //超出了结尾
                    {
                        break;
                    }

                    listComboTime.Add(t);
                }
            }
        }

        {
            //尾combo, noteEnd
            listComboTime.Add(noteEndTime);
        }

        return listComboTime.Count - count;
    }

    public int GetComboCount(int comboPerBeatInHold, EnumLineType noteType)
    {
        int cnt = 0;
        List<MusicBeatNoteJson> notes = GetNotes(noteType);

        for (int i = 0; i < notes.Count; ++i)
        {
            MusicBeatNoteJson note = notes[i];
            if (notes[i].exType == (int) EnumBeatItemTypeEx.eNone)
            {
                cnt += 1;
            }
            else if (notes[i].exType == (int)EnumBeatItemTypeEx.eHoldBegin)
            {
                MusicBeatNoteJson noteEnd = notes[i + 1];

                List<float> list = new List<float>();
                cnt += GetHoldComboCout(note, noteEnd, comboPerBeatInHold, ref list);

                i++;
            }
            else
            {
                MyDebug.LogError("出错了, 找裴少 : GetComboCount");
            }
        }

        return cnt;
    }

    public float GetDiff()
    {
        float diff = 0f;
        if (upNotes.Count > 1)
        {
            MusicBeatNoteJson note_first = upNotes[0];
            float time_first = GetNoteRunTime(note_first);

            MusicBeatNoteJson note_last = upNotes[upNotes.Count - 1];
            float time_last = GetNoteRunTime(note_last);

            float dura = time_last - time_first;

            diff = (int)((dura * 1000) / upNotes.Count);
        }

        return diff;
    }

    public int GetCountByType(EnumBeatItemType type, EnumLineType noteType)
    {
        int ret = 0;
        int iType = (int)type;
        int iexType = (int)EnumBeatItemTypeEx.eHoldEnd;
        List<MusicBeatNoteJson> notes = GetNotes(noteType);

        for (int i = 0; i < notes.Count; ++i)
        {
            //长按的结尾不算
            if (notes[i].exType == iexType)
                continue;

            if (notes[i].type == iType)
                ret++;
        }

        return ret;
    }

    public bool Check(int comboPerBeatInHold, ref string tip)
    {
        bool ret = true;
        tip = "";

        do
        {
            ret = ChecNote(comboPerBeatInHold, EnumLineType.UpNote, ref tip);
            if(!ret)
            {
                break;
            }
            ret = ChecNote(comboPerBeatInHold, EnumLineType.DownNote, ref tip);
            if(!ret)
            {
                break;
            }
        } while (false);
        
        return ret;
    }
    
    private bool ChecNote(int comboPerBeatInHold, EnumLineType noteType, ref string tip)
    {
        bool bRet = true;
        string warningStr = string.Empty;
        string errorStr = string.Empty;
        List<MusicBeatNoteJson> notes = GetNotes(noteType);
        string noteLineTitle = string.Empty;
        if(noteType == EnumLineType.UpNote)
        {
            noteLineTitle = "上轨";
        }
        else if(noteType == EnumLineType.DownNote)
        {
            noteLineTitle = "下轨";
        }

        do
        {
            for (int i = 0; i < notes.Count; ++i)
            {
                MusicBeatNoteJson note = notes[i];

                if (note == null)
                {
                    warningStr += string.Format("\n{0}第[{1}]个音符为空", noteLineTitle, i);
                    upNotes.RemoveAt(i--);
                    continue;
                }

                if (note.beatIndex < 0 || note.beatIndex >= beatCount)
                {
                    warningStr += string.Format("\n{0}第[{1}]个音符的节拍索引错误", noteLineTitle, i);
                    upNotes.RemoveAt(i--);
                    continue;
                }

                MusicBeatJson beat = beats[note.beatIndex];

                if (note.noteIndex < 0 || note.noteIndex >= beat.split)
                {
                    warningStr += string.Format("\n{0}第[{1}]个音符的音符索引[{2}]错误, 节拍[{3}]为[{4}]分音", noteLineTitle, i, note.noteIndex, note.beatIndex, beat.split);
                    upNotes.RemoveAt(i--);
                    continue;
                }

                if (note.type == (int)EnumBeatItemType.eNone)
                {
                    warningStr += string.Format("\n{0}第[{1}]个音符的类型[{2}]错误", noteLineTitle, i, note.type);
                    upNotes.RemoveAt(i--);
                    continue;
                }
            }

            //前4拍要留白
            if (notes != null && notes.Count > 0 && notes[0].beatIndex < 4)
            {
                warningStr += string.Format("\n{0}前4拍必须留白", noteLineTitle);
                bRet = false;
                break;
            }

            int count = notes.Count;
            for (int i = 0; i < count; ++i)
            {
                MusicBeatNoteJson note = notes[i];
                if (note.exType == (int)EnumBeatItemTypeEx.eHoldBegin)
                {
                    int ii = i + 1;
                    bool succ = false;
                    if (ii < count)
                    {
                        MusicBeatNoteJson next = notes[i + 1];
                        if (next.exType == (int)EnumBeatItemTypeEx.eHoldEnd)
                        {
                            succ = true;
                        }
                    }

                    if (!succ)
                    {
                        errorStr += string.Format("\n{0}Hold没有结束. 节拍=[{1}] 索引=[{2}]", noteLineTitle, note.beatIndex, note.noteIndex);
                        bRet = false;
                        continue;
                    }
                }
            }
            if(!bRet)
            {
                break;
            }

            for (int i = 0; i < count; ++i)
            {
                MusicBeatNoteJson note = notes[i];
                if (note.exType == (int)EnumBeatItemTypeEx.eHoldEnd)
                {
                    int ii = i - 1;
                    bool succ = false;
                    if (ii >= 0)
                    {
                        MusicBeatNoteJson next = notes[ii];

                        if (next.exType == (int)EnumBeatItemTypeEx.eHoldBegin)
                        {
                            succ = true;
                        }
                    }

                    if (!succ)
                    {
                        errorStr += string.Format("\n{0}Hold没有开始. 节拍=[{1}] 索引=[{2}]",noteLineTitle, note.beatIndex, note.noteIndex);
                        bRet = false;
                        continue;
                    }
                }
            }
            if (!bRet)
            {
                break;
            }


            //到目前为止,如果以一切ＯＫ
            //检查长按的间隔  //Hold的间隔  不能小于1格  不能大于10拍
            for (int i = 0; i < (count - 1); ++i)
            {
                MusicBeatNoteJson note = notes[i];
                if (note.exType != (int)EnumBeatItemTypeEx.eHoldBegin)
                    continue;

                MusicBeatNoteJson next = notes[i + 1];
                if (next.exType != (int)EnumBeatItemTypeEx.eHoldEnd)
                    continue;

                bool succ = false;

                MusicBeatJson beat1 = beats[note.noteIndex];
                MusicBeatJson beat2 = beats[next.noteIndex];
                int split = beat1.split;
                if (split != beat2.split)
                {
                    // 最小公倍数
                    split = BuilderUtils.LCM(split, beat2.split);
                }

                int multiple1 = split / beat1.split;
                int multiple2 = split / beat2.split;

                int index1 = note.beatIndex * split + note.noteIndex * multiple1;
                int index2 = next.beatIndex * split + next.noteIndex * multiple2;

                //10拍的长度
                int len = split * 10;

                bool condition1 = (index2 - index1) >= 2; // 间距不小于1格, 不能挨着
                bool condition2 = (index2 - index1) <= len; //不超过10拍

                if (condition1 && condition2)
                {
                    succ = true;
                }

                if (!succ)
                {
                    errorStr += string.Format("\n{0}Hold长度错误. 节拍=[{1}] 索引=[{2}]", noteLineTitle, note.beatIndex, note.noteIndex);
                    bRet = false;
                    break;
                }
            }
            if(!bRet)
            {
                break;
            }

            //计算diff值(参考策划文档:diff值 = 操作小节总时长/音符总数)
            //操作小节总时长:第一个音符到最后一个音符的时间间隔
            //音符总数
            float diff = 0f;
            if (notes.Count > 1)
            {
                diff = GetDiff();
            }

            tip += string.Format("\n{0}音符统计:", noteLineTitle);

            tip += string.Format("\n普通Note={0}", GetCountByType(EnumBeatItemType.eNormaNote, noteType));
            tip += string.Format("\n反向Note={0}", GetCountByType(EnumBeatItemType.eReverseNote, noteType));
            tip += string.Format("\nBossNote={0}", GetCountByType(EnumBeatItemType.eBossNote, noteType));
            tip += string.Format("\n连击Note={0}", GetCountByType(EnumBeatItemType.eLaunchNote, noteType));
            tip += string.Format("\n长按Note={0}", GetCountByType(EnumBeatItemType.eLaunchNote, noteType));
            tip += string.Format("\n长按Note ={0} \n音符数={1} \ndiff={2} ", GetComboCount(comboPerBeatInHold, noteType), notes.Count, diff);

            tip += string.Format("\n---------------------分割线--------------------------");

        } while (false);

        if(!bRet)
        {
            tip += string.Format("错误{0}", errorStr);
        }

        if (!string.IsNullOrEmpty(warningStr))
            tip += warningStr;

        return bRet;
    }
}

[Serializable]
public class MusicBeatJson
{
    public int split;
    public float noteTime;
    private int noteIdTpl;
    public int NoteIdTpl
    {
        set;
        get;
    }
}

public enum MusicBeatNoteIdentityType_Noraml
{
    Type_Normal = 0,
    Type_FromBoss_Skill_1,
    Type_FromBoss_Skill_2,
    Type_Recover,
    Type_Reward,
}

public enum  MusicBeatNoteIdentityType_Boss
{
    Type_Group1,
    Type_Group2,    
}

public enum MusicBeatNoteIdentityType_Launch
{
    Type_LaunchDown,
    Type_LaunchUp,
}

[Serializable]
public class MusicBeatNoteJson : IComparable, ICloneable
{
    public int noteID;
    public int noteIdentity;
    public int beatIndex;
    public int noteIndex;


    public int type;
    public int exType;

    public int yeah;

    public int CompareTo(object o)
    {
        MusicBeatNoteJson m = o as MusicBeatNoteJson;
        if (m == null)
            return 0;

        if (beatIndex < m.beatIndex)
        {
            return -1;
        }
        else if (beatIndex == m.beatIndex)
        {
            if (noteIndex < m.noteIndex)
                return -1;
            else if (noteIndex == m.noteIndex)
                return 0;
            else
                return 1;
        }
        else
        {
            return 1;
        }
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}

[Serializable]
public class MusicBeatFloorJson : IComparable, ICloneable
{
    public int beatIndex;
    public int noteIndex;
    public int heightlevel = 0;
    public int floorId = 0;

    private List<int> list = new List<int>();

    public void AddTrigger(int t)
    {
        if (list.Contains(t))
            return;

        list.Add(t);
        list.Sort();
    }

    public void RemoveTrigger(int t)
    {
        list.Remove(t);
    }

    public bool IsEmpty()
    {
        return list.Count == 0;
    }

    public int Count()
    {
        return list.Count;
    }

    public override string ToString()
    {
        string ret = "";
        if (list.Count > 0)
            ret = list[0].ToString();

        if (list.Count > 1)
            ret += ("|...");

        return ret;
    }

    public void CopyFrom(MusicBeatFloorJson target)
    {
        heightlevel = target.heightlevel;
        floorId = target.floorId;
    }
    public int CompareTo(object o)
    {
        MusicBeatFloorJson m = o as MusicBeatFloorJson;
        if (m == null)
            return 0;

        if (beatIndex < m.beatIndex)
        {
            return -1;
        }
        else if (beatIndex == m.beatIndex)
        {
            if (noteIndex < m.noteIndex)
                return -1;
            else if (noteIndex == m.noteIndex)
                return 0;
            else
                return 1;
        }
        else
        {
            return 1;
        }
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}

public enum MusicBeatObstacleIdentityType
{
    Type_Obstacle = 0,
    Type_Fly_Y,
    Type_Fly_X,
}

[Serializable]
public class MusicBeatObstacleJosn : IComparable, ICloneable
{
    public int beatIndex;
    public int noteIndex;
    public int obstacleID;
    public int identity;


    public int CompareTo(object o)
    {
        MusicBeatObstacleJosn m = o as MusicBeatObstacleJosn;
        if (m == null)
            return 0;

        if (beatIndex < m.beatIndex)
        {
            return -1;
        }
        else if (beatIndex == m.beatIndex)
        {
            if (noteIndex < m.noteIndex)
                return -1;
            else if (noteIndex == m.noteIndex)
                return 0;
            else
                return 1;
        }
        else
        {
            return 1;
        }
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }

}

public enum MusicBeatEnvIdentityType
{
    Type_BossAppear = 0,
    Type_BossDisappera,
    Type_SpeedScale,
    Type_UserGuide,
    Type_Qte_Start,
    Type_Qte_End,
}

public enum UserGuideType
{
    Type_Attack_Note_Appear = 0,
    Type_Jump_Note_Appear,
    Type_Jump_Terrain_Appear,
    Type_HoldAttack_Note_Appear,
    Type_HoldJump_Appear,
    Type_Combo_Boss_Appear,
    Type_CloseUserGuide,
    Type_Attack_Note,
    Type_Jump_Note,
    Type_Jump_Terrain,
    Type_HoldAttack_Note,
    Type_HoldJump,
    Type_Combo_Boss,

    Type_GameStart_Title,
    Type_GameStart_Title2,
    Type_GameStart_Title3,

    //Type_Jump_Title,
    //Type_LongJump_Title,
    //Type_Attack_Title,
    //Type_LongAttack_Title,
    //Type_Combo_Title,
    Type_GuideTitle,
    Type_GuideTitle_End,


    Type_GameEnd_Title1,
    Type_GameEnd_Title2,
    //Type_BossShow_Title,

}

[Serializable]
public class MusicBeatEnvJson : IComparable, ICloneable
{
    public int beatIndex;
    public int noteIndex;
    public float userData = 1;
    public string userNote = "";
    public List<int> list = new List<int>();

    public void AddTrigger(int t)
    {
        if (list.Contains(t))
            return;

        list.Add(t);
        list.Sort();
    }

    public void RemoveTrigger(int t)
    {
        list.Remove(t);
    }

    public bool IsEmpty()
    {
        return list.Count == 0;
    }

    public int Count()
    {
        return list.Count;
    }

    public override string ToString()
    {
        string ret = "";
        if (list.Count > 0)
            ret = list[0].ToString();

        if (list.Count > 1)
            ret += ("|...");

        return ret;
    }

    public int CompareTo(object o)
    {
        MusicBeatEnvJson m = o as MusicBeatEnvJson;
        if (m == null)
            return 0;

        if (beatIndex < m.beatIndex)
        {
            return -1;
        }
        else if (beatIndex == m.beatIndex)
        {
            if (noteIndex < m.noteIndex)
                return -1;
            else if (noteIndex == m.noteIndex)
                return 0;
            else
                return 1;
        }
        else
        {
            return 1;
        }
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}

public class EditorOp
{
    public int splitFromBeat;
    public int splitToBeat;
    public int splitNum;
    public int splitIndex;

    public int copyRangeStart;
    public int copyRangeEnd;
    public int copyToBeat;
    public List<MusicBeatNoteJson> copyBufferUp;
    public List<MusicBeatNoteJson> copyBufferDown;

    public float chartLength;
    public float musicOffset;
    public float bpm;

    public int trigger;
    public MusicBeatEnvIdentityType envIndenty;
    public UserGuideType userGuideType;
     
    public MusicBeatNoteIdentityType_Boss bossNoteIndentityTypeTpl = MusicBeatNoteIdentityType_Boss.Type_Group1;
    public void Clear()
    {
        splitFromBeat = splitToBeat = 0;

        copyRangeStart = copyRangeEnd = 0;
        copyToBeat = 0;
        if (copyBufferUp != null)
            copyBufferUp.Clear();

        if (copyBufferDown != null)
            copyBufferDown.Clear();

        chartLength = musicOffset = bpm = 0f;

        trigger = 0;
    }
}

public class MusicBeatUndo
{
    List<string> _list = new List<string>();
    const int Max = 20;
    public void Clear()
    {
        _list.Clear();
    }

    public void Push(string content)
    {
        _list.Add(content);

        if (_list.Count > Max)
        {
            _list.RemoveAt(0);
        }
    }

    public string Pop()
    {
        if (_list.Count > 0)
        {
            int last = _list.Count - 1;
            string ret = _list[last];
            _list.RemoveAt(last);
            return ret;
        }

        return null;
    }

    public int Count { get { return _list.Count; } }
}