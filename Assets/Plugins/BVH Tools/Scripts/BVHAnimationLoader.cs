using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using System.Text;
using System.Threading.Tasks;



public class BVHAnimationLoader : MonoBehaviour
{
    [Header("Loader settings")]
    [Tooltip("This is the target avatar for which the animation should be loaded. Bone names should be identical to those in the BVH file and unique. All bones should be initialized with zero rotations. This is usually the case for VRM avatars.")]
    public Animator targetAvatar;
    [Tooltip("This is the path to the BVH file that should be loaded. Bone offsets are currently being ignored by this loader.")]
    public string filename;
    // [Tooltip("이 옵션을 설정하면 BVH 파일은 일반 BVH 규칙 대신 Z축이 위쪽이고 Y축이 앞쪽인 것으로 간주됩니다.")]
    [Tooltip("When this option is set, the BVH file will be assumed to have the Z axis as up and the Y axis as forward instead of the normal BVH conventions.")]
    public bool blender = true;
    [Tooltip("When this flag is set, the frame time in the BVH time will be used to determine the frame rate instead of using the one given below.")]
    public bool respectBVHTime = true;
    [Tooltip("If the flag above is disabled, the frame rate given in the BVH file will be overridden by this value.")]
    public float frameRate = 60.0f;
    [Tooltip("This is the name that will be set on the animation clip. Leaving this empty is also okay.")]
    public string clipName;
    [Header("Advanced settings")]
    [Tooltip("When this option is enabled, standard Unity humanoid bone names will be mapped to the corresponding bones of the skeleton.")]
    public bool standardBoneNames = true;
    [Tooltip("When this option is disabled, bone names have to match exactly.")]
    public bool flexibleBoneNames = true;
    [Tooltip("This allows you to give a mapping from names in the BVH file to actual bone names. If standard bone names are enabled, the target names may also be Unity humanoid bone names. Entries with empty BVH names will be ignored.")]
    public FakeDictionary[] boneRenamingMap = null;
    [Header("Animation settings")]
    [Tooltip("When this option is set, the animation start playing automatically after being loaded.")]
    public bool autoPlay = false;
    [Tooltip("When this option is set, the animation will be loaded and start playing as soon as the script starts running. This also implies the option above being enabled.")]
    public bool autoStart = false;
    [Header("Animation")]
    [Tooltip("This is the Animation component to which the clip will be added. If left empty, a new Animation component will be added to the target avatar.")]
    public Animation anim;
    [Tooltip("This field can be used to read out the the animation clip after being loaded. A new clip will always be created when loading.")]
    public AnimationClip clip;

    static private int clipCount = 0;
    private BVHParser bp = null;
    private Transform rootBone;
    private string prefix;
    private int frames;
    private Dictionary<string, string> pathToBone;
    private Dictionary<string, string[]> boneToMuscles;
    private Dictionary<string, Transform> nameMap;
    private Dictionary<string, string> renamingMap;

    [Serializable]
    public struct FakeDictionary
    {
        public string bvhName;
        public string targetName;
    }

    // BVH to Unity
    private Quaternion fromEulerZXY(Vector3 euler)
    {
        return Quaternion.AngleAxis(euler.z, Vector3.forward) * Quaternion.AngleAxis(euler.x, Vector3.right) * Quaternion.AngleAxis(euler.y, Vector3.up);
    }

    private float wrapAngle(float a)
    {
        
        if (a > 180f)
        {
            return a - 360f;
        }
        if (a < -180f)
        {
            return 360f + a;
        }
        return a;
    }

    private string flexibleName(string name)
    {
        if (!flexibleBoneNames)
        {
            return name;
        }
        name = name.Replace(" ", "");
        name = name.Replace("_", "");
        name = name.ToLower();
        return name;
    }

    private Transform getBoneByName(string name, Transform transform, bool first)
    {
        string targetName = flexibleName(name);
        //Debug.Log("getBoneByName : targetName : " + targetName);
        if (renamingMap.ContainsKey(targetName))
        {
            targetName = flexibleName(renamingMap[targetName]);
        }
        if (first)
        {
            if (flexibleName(transform.name) == targetName)
            {
                return transform;
            }
            if (nameMap.ContainsKey(targetName) && nameMap[targetName] == transform)
            {
                return transform;
            }
        }
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (flexibleName(child.name) == targetName)
            {
                return child;
            }
            if (nameMap.ContainsKey(targetName) && nameMap[targetName] == child)
            {
                return child;
            }
        }
        throw new InvalidOperationException("Could not find bone \"" + name + "\" under bone \"" + transform.name + "\".");
    }

    int text = 0;
    private void getCurves(string path, BVHParser.BVHBone node, Transform bone, bool first)
    {
        //Debug.Log(++text + " node.name : " + node.name  + ", bone.name : " + bone.name);
        bool posX = false;
        bool posY = false;
        bool posZ = false;
        bool rotX = false;
        bool rotY = false;
        bool rotZ = false;

        float[][] values = new float[6][];
        Keyframe[][] keyframes = new Keyframe[7][];
        string[] props = new string[7];
        Transform nodeTransform = getBoneByName(node.name, bone, first); // 필요한 뼈대이름에 대한 트랜스폼 가져옴
        //Debug.Log(text + " : nodeTransform : " + nodeTransform);

        if (path != prefix)
        {
            path += "/";
        }
        if (rootBone != targetAvatar.transform || !first)
        {
            path += nodeTransform.name;
        }
        // 모든 채널에서 두 개의 vector3로 수집하고 좌표계 변환을 반전한 다음 여기에서 키프레임을 만들도록 변경해야 합니다.
        // This needs to be changed to gather from all channels into two vector3, invert the coordinate system transformation and then make keyframes from it
        for (int channel = 0; channel < 6; channel++)
        {
            if (!node.channels[channel].enabled)
            {
                continue;
            }

            switch (channel)
            {
                case 0:
                    posX = true;
                    props[channel] = "localPosition.x";
                    break;
                case 1:
                    posY = true;
                    props[channel] = "localPosition.y";
                    break;
                case 2:
                    posZ = true;
                    props[channel] = "localPosition.z";
                    break;
                case 3:
                    rotX = true;
                    props[channel] = "localRotation.x";
                    break;
                case 4:
                    rotY = true;
                    props[channel] = "localRotation.y";
                    break;
                case 5:
                    rotZ = true;
                    props[channel] = "localRotation.z";
                    break;
                default:
                    channel = -1;
                    break;
            }
            if (channel == -1)
            {
                continue;
            }

            keyframes[channel] = new Keyframe[frames];
            values[channel] = node.channels[channel].values;
            // position은 0,1,2 rotation은 3,4,5 (x,y,z 순)
            //Debug.Log(" nodeTransform : " + nodeTransform + ", node.channels["+ channel  + "].values : " + node.channels[channel].values);
            if (rotX && rotY && rotZ && keyframes[6] == null)
            {
                keyframes[6] = new Keyframe[frames];
                props[6] = "localRotation.w";
                //Debug.Log(" nodeTransform : " + nodeTransform); // 조인트별로 w 다 채움
            }
        }

        float time = 0f;
        if (posX && posY && posZ)
        {
            // position x,y,z 다 있는 경우는 root 인 힙 뿐. 즉, hip 만 여기 탐
            Vector3 offset;
            if (blender) // blender은 항상 false 탐
            {
                offset = new Vector3(-node.offsetX, node.offsetZ, -node.offsetY);
            }
            else
            {
                offset = new Vector3(-node.offsetX, node.offsetY, node.offsetZ);
                Debug.Log("node.offsetX : " + node.offsetX + ", node.offsetY : " + node.offsetY);
            }
            for (int i = 0; i < frames; i++)
            {
                time += 1f / frameRate;
                keyframes[0][i].time = time;
                keyframes[1][i].time = time;
                keyframes[2][i].time = time;
                if (blender)
                {
                    keyframes[0][i].value = -values[0][i];
                    keyframes[1][i].value = values[2][i];
                    keyframes[2][i].value = -values[1][i];
                }
                else
                {
                    keyframes[0][i].value = -values[0][i];
                    keyframes[1][i].value = values[1][i];
                    keyframes[2][i].value = values[2][i];
                }
                if (first) // 최초에 한번 불릴 때만 세팅 => 움직임과, 스케일에 따른 프레임 조정 역할
                {
                    Vector3 bvhPosition = bone.transform.parent.InverseTransformPoint(new Vector3(keyframes[0][i].value, keyframes[1][i].value, keyframes[2][i].value) + targetAvatar.transform.position + offset);
                    //Debug.Log("  bvhPosition.x : " + bvhPosition.x);
                    //Debug.Log("  targetAvatar.transform.localScale.x : " + targetAvatar.transform.localScale.x);
                    keyframes[0][i].value = bvhPosition.x * targetAvatar.transform.localScale.x;
                    //Debug.Log("  keyframes[0]["+i+"].value : " + keyframes[0][i].value);
                    keyframes[1][i].value = bvhPosition.y * targetAvatar.transform.localScale.y;
                    keyframes[2][i].value = bvhPosition.z * targetAvatar.transform.localScale.z;
                }
            }
            if (first) // 최초에 한번 불릴 때만
            {
                clip.SetCurve(path, typeof(Transform), props[0], new AnimationCurve(keyframes[0]));
                clip.SetCurve(path, typeof(Transform), props[1], new AnimationCurve(keyframes[1]));
                clip.SetCurve(path, typeof(Transform), props[2], new AnimationCurve(keyframes[2]));
            }
            else
            {
                Debug.LogWarning("Position information on bones other than the root bone is currently not supported and has been ignored. If you exported this file from Blender, please tick the \"Root Translation Only\" option next time.");
            }
        }

        time = 0f;
        if (rotX && rotY && rotZ) // rotation x,y,z 다 있는 경우 => 힙을 제외한 모든 관절
        {
            Quaternion oldRotation = bone.transform.rotation;
            //Debug.Log("bone.transform : " + bone.transform);
            //Debug.Log("bone.transform.rotation.x : " + bone.transform.rotation.x);
            //Debug.Log("bone.transform.rotation.x : " + bone.transform.localRotation.x);
            for (int i = 0; i < frames; i++)
            {
                // wrapAngle(values[3][i] : 세번째 rotation, wrapAngle(values[4][i] : 두번째, wrapAngle(values[5][i] : 첫번째
                // test_freebvh 상 세번째 rotatiln은 X, 두번째는 Y, 첫번 째는 Z
                //Debug.Log("wrapAngle(values[3]["+i+"]) : " + wrapAngle(values[3][i]) + ", wrapAngle(values[4][" + i + "]) : " + wrapAngle(values[4][i]));
                //Debug.Log("oldRotation.x : " + oldRotation.x + ", oldRotation.y : " + oldRotation.y);
                // 위 부분 파서에서 처리해주므로 결론적으로 x,y,z 순서대로 들어가게됨
                //Vector3 eulerBVH = new Vector3(wrapAngle(values[3][i]), wrapAngle(values[4][i]), wrapAngle(values[5][i]));
                Vector3 eulerBVH = new Vector3(wrapAngle(values[3][i])+ oldRotation.x, wrapAngle(values[4][i]), wrapAngle(values[5][i]));
                Quaternion rot = fromEulerZXY(eulerBVH);
                // AngleAxis 메서드는 axis 중심으로 angle 만큼 회전시킨 quaternion 반환
                rot =  Quaternion.AngleAxis(eulerBVH.z, Vector3.forward) * Quaternion.AngleAxis(eulerBVH.x, Vector3.right) * Quaternion.AngleAxis(eulerBVH.y, Vector3.up);

                if (blender)
                {
                    keyframes[3][i].value = rot.x;
                    keyframes[4][i].value = -rot.z;
                    keyframes[5][i].value = rot.y;
                    keyframes[6][i].value = rot.w;

                    //rot2 = new Quaternion(rot.x, -rot.z, rot.y, rot.w); 원래 주석
                }
                else
                {
                    //rot = new Quaternion(rot.x + bone.transform.localRotation.x, -rot.y + bone.transform.localRotation.y, -rot.z + bone.transform.localRotation.z, rot.w + bone.transform.localRotation.w);

                    Debug.Log("bone.transform : " + bone.transform + ",  rot.y : " + rot.y);
                    keyframes[3][i].value = rot.x;
                    keyframes[4][i].value = -rot.y;
                    keyframes[5][i].value = - rot.z;
                    keyframes[6][i].value = rot.w;
                    //rot2 = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
                }
                if (first)
                {
                    bone.transform.rotation = new Quaternion(keyframes[3][i].value, keyframes[4][i].value, keyframes[5][i].value, keyframes[6][i].value);
                    keyframes[3][i].value = bone.transform.localRotation.x;
                    keyframes[4][i].value = bone.transform.localRotation.y;
                    keyframes[5][i].value = bone.transform.localRotation.z;
                    keyframes[6][i].value = bone.transform.localRotation.w;
                }
                /*Vector3 euler = rot2.eulerAngles;

                keyframes[3][i].value = wrapAngle(euler.x);
                keyframes[4][i].value = wrapAngle(euler.y);
                keyframes[5][i].value = wrapAngle(euler.z);*/

                time += 1f / frameRate;
                keyframes[3][i].time = time;
                keyframes[4][i].time = time;
                keyframes[5][i].time = time;
                keyframes[6][i].time = time;
            }
            //bone.transform.rotation = oldRotation;
            clip.SetCurve(path, typeof(Transform), props[3], new AnimationCurve(keyframes[3]));
            clip.SetCurve(path, typeof(Transform), props[4], new AnimationCurve(keyframes[4]));
            clip.SetCurve(path, typeof(Transform), props[5], new AnimationCurve(keyframes[5]));
            clip.SetCurve(path, typeof(Transform), props[6], new AnimationCurve(keyframes[6]));
        }

        foreach (BVHParser.BVHBone child in node.children)
        {
            getCurves(path, child, nodeTransform, false);
        }
    }

    public static string getPathBetween(Transform target, Transform root, bool skipFirst, bool skipLast)
    {
        //Debug.Log("Transform target : " + target.name + ", root.name : " + root.name + ", root.cjildCount : " + root.childCount);
        if (root == target)
        {
            if (skipLast)
            {
                return "";
            }
            else
            {
                return root.name;
            }
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (target.IsChildOf(child))
            {
                if (skipFirst)
                {
                    return getPathBetween(target, child, false, skipLast);
                }
                else
                {
                    return root.name + "/" + getPathBetween(target, child, false, skipLast);
                }
            }
        }

        throw new InvalidOperationException("No path between transforms " + target.name + " and " + root.name + " found.");
    }

    private void getTargetAvatar()
    {
        if (targetAvatar == null)
        {
            targetAvatar = GetComponent<Animator>();
        }
        if (targetAvatar == null)
        {
            throw new InvalidOperationException("No target avatar set.");
        }

    }

    public void loadAnimation()
    {
        getTargetAvatar();

        if (bp == null)
        {
            throw new InvalidOperationException("No BVH file has been parsed.");
        }

        if (nameMap == null)
        {
            if (standardBoneNames)
            {
                Dictionary<Transform, string> boneMap;
                BVHRecorder.populateBoneMap(out boneMap, targetAvatar);
                nameMap = boneMap.ToDictionary(kp => flexibleName(kp.Value), kp => kp.Key);
            }
            else
            {
                nameMap = new Dictionary<string, Transform>();
            }
        }

        renamingMap = new Dictionary<string, string>();
        foreach (FakeDictionary entry in boneRenamingMap) // 안탐
        {
            if (entry.bvhName != "" && entry.targetName != "")
            {
                renamingMap.Add(flexibleName(entry.bvhName), flexibleName(entry.targetName));
                Debug.Log("안탐 entry.bvhName : " + entry.bvhName);
            }
        }

        Queue<Transform> transforms = new Queue<Transform>();

        transforms.Enqueue(targetAvatar.transform);
        string targetName = flexibleName(bp.root.name); // Hips
        //Debug.Log("targetName : " + targetName);
        
        if (renamingMap.ContainsKey(targetName)) // 안탐
        {
            targetName = flexibleName(renamingMap[targetName]);
            Debug.Log("안탐");
        }
        while (transforms.Any())
        {
            Transform transform = transforms.Dequeue();
            //Debug.Log("transforms.Dequeue() : " + transform); // Hips
            if (flexibleName(transform.name) == targetName)
            {
                rootBone = transform;
                break;
            }
            if (nameMap.ContainsKey(targetName) && nameMap[targetName] == transform)
            {
                rootBone = transform;
                break;
            }
            for (int i = 0; i < transform.childCount; i++)
            { // Hip 까지만 탐
                Transform temp = transform.GetChild(i);
                //Debug.Log("temp : " + temp);
                //Debug.Log(i + " has temp.childCount : " + temp.childCount);
                transforms.Enqueue(temp);
                //transforms.Enqueue(transform.GetChild(i));
            }
        }
        if (rootBone == null)
        {
            rootBone = BVHRecorder.getRootBone(targetAvatar);
            Debug.LogWarning("Using \"" + rootBone.name + "\" as the root bone.");
        }
        if (rootBone == null)
        {
            throw new InvalidOperationException("No root bone \"" + bp.root.name + "\" found.");
        }
        //Debug.Log("rootBone : " + rootBone); // Hips
        frames = bp.frames;
        //Debug.Log("frames : " + frames); // frames : ~~ 에 나오눈 갯수
        clip = new AnimationClip();
        clip.name = "BVHClip (" + (clipCount++) + ")";
        if (clipName != "")
        {
            clip.name = clipName;
        }
        clip.legacy = true;
        prefix = getPathBetween(rootBone, targetAvatar.transform, true, true); // Hip 까지만 타는듯

        Vector3 targetAvatarPosition = targetAvatar.transform.position;
        Quaternion targetAvatarRotation = targetAvatar.transform.rotation;
        targetAvatar.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
        targetAvatar.transform.rotation = Quaternion.identity;

        //Debug.Log("prefix : " + prefix); // 안나옴암것도
        //Debug.Log("bp.root.channelNumber : " + bp.root.channelNumber); // bp.root.name : Hips, bp.root.children.Count:3, channelNumber:6
        //Debug.Log("rootBone : " + rootBone); // mixamorig:Hips (UnityEngine.Transform)
        text = 0;
        getCurves(prefix, bp.root, rootBone, true);

        targetAvatar.transform.position = targetAvatarPosition;
        targetAvatar.transform.rotation = targetAvatarRotation;

        clip.EnsureQuaternionContinuity();
        if (anim == null)
        {
            anim = targetAvatar.gameObject.GetComponent<Animation>();
            if (anim == null)
            {
                anim = targetAvatar.gameObject.AddComponent<Animation>();
            }
        }
        anim.AddClip(clip, clip.name);
        anim.clip = clip;
        anim.playAutomatically = autoPlay;
        if (autoPlay)
        {
            anim.Play(clip.name);
        }
    }

    // This function doesn't call any Unity API functions and should be safe to call from another thread
    public void parse(string bvhData)
    {
        if (respectBVHTime)
        {
            bp = new BVHParser(bvhData);
            frameRate = 1f / bp.frameTime;
        }
        else
        {
            bp = new BVHParser(bvhData, 1f / frameRate);
        }
        //Debug.Log("bp : " + bp);
    }

    // This function doesn't call any Unity API functions and should be safe to call from another thread
    public void parseFile()
    {
        parse(File.ReadAllText(filename));
    }

    public void playAnimation()
    {
        if (bp == null)
        {
            throw new InvalidOperationException("No BVH file has been parsed.");
        }
        if (anim == null || clip == null)
        {
            loadAnimation();
        }
        anim.Play(clip.name);
    }

    public void stopAnimation()
    {
        if (clip != null)
        {
            if (anim.IsPlaying(clip.name))
            {
                anim.Stop();
            }
        }
    }

    void Start()
    {
        if (autoStart)
        {
            autoPlay = true;
            parseFile();
            loadAnimation();
        }
    }
}