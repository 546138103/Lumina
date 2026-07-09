#define FLIP // Comment out this line to flip the landmarks (internally).
// NOTE: image = cv2.flip(image, 1) in the Python side may also be of interest to you as well.

#if FLIP
public enum Landmark
{
    NOSE = 0,             // 鼻尖
    LEFT_EYE_INNER = 4,   // 左眼内侧
    LEFT_EYE = 5,         // 左眼中心
    LEFT_EYE_OUTER = 6,   // 左眼外侧
    RIGHT_EYE_INNER = 1,  // 右眼内侧
    RIGHT_EYE = 2,        // 右眼中心
    RIGHT_EYE_OUTER = 3,  // 右眼外侧
    LEFT_EAR = 8,         // 左耳
    RIGHT_EAR = 7,        // 右耳
    MOUTH_LEFT = 10,      // 左嘴角
    MOUTH_RIGHT = 9,      // 右嘴角
    LEFT_SHOULDER = 12,   // 左肩
    RIGHT_SHOULDER = 11,  // 右肩
    LEFT_ELBOW = 14,      // 左肘
    RIGHT_ELBOW = 13,     // 右肘
    LEFT_WRIST = 16,      // 左手腕
    RIGHT_WRIST = 15,     // 右手腕
    LEFT_PINKY = 18,      // 左手小拇指
    RIGHT_PINKY = 17,     // 右手小拇指
    LEFT_INDEX = 20,      // 左手食指
    RIGHT_INDEX = 19,     // 右手食指
    LEFT_THUMB = 22,      // 左手拇指
    RIGHT_THUMB = 21,     // 右手拇指
    LEFT_HIP = 24,        // 左髋
    RIGHT_HIP = 23,       // 右髋
    LEFT_KNEE = 26,       // 左膝
    RIGHT_KNEE = 25,      // 右膝
    LEFT_ANKLE = 28,      // 左脚踝
    RIGHT_ANKLE = 27,     // 右脚踝
    LEFT_HEEL = 30,       // 左脚跟
    RIGHT_HEEL = 29,      // 右脚跟
    LEFT_FOOT_INDEX = 32, // 左脚尖
    RIGHT_FOOT_INDEX = 31,// 右脚尖
    NONE = 40             // 无关键点
}
#else
public enum Landmark
{
    NOSE = 0,             // 鼻尖
    LEFT_EYE_INNER = 1,   // 左眼内侧
    LEFT_EYE = 2,         // 左眼中心
    LEFT_EYE_OUTER = 3,   // 左眼外侧
    RIGHT_EYE_INNER = 4,  // 右眼内侧
    RIGHT_EYE = 5,        // 右眼中心
    RIGHT_EYE_OUTER = 6,  // 右眼外侧
    LEFT_EAR = 7,         // 左耳
    RIGHT_EAR = 8,        // 右耳
    MOUTH_LEFT = 9,       // 左嘴角
    MOUTH_RIGHT = 10,     // 右嘴角
    LEFT_SHOULDER = 11,   // 左肩
    RIGHT_SHOULDER = 12,  // 右肩
    LEFT_ELBOW = 13,      // 左肘
    RIGHT_ELBOW = 14,     // 右肘
    LEFT_WRIST = 15,      // 左手腕
    RIGHT_WRIST = 16,     // 右手腕
    LEFT_PINKY = 17,      // 左手小拇指
    RIGHT_PINKY = 18,     // 右手小拇指
    LEFT_INDEX = 19,      // 左手食指
    RIGHT_INDEX = 20,     // 右手食指
    LEFT_THUMB = 21,      // 左手拇指
    RIGHT_THUMB = 22,     // 右手拇指
    LEFT_HIP = 23,        // 左髋
    RIGHT_HIP = 24,       // 右髋
    LEFT_KNEE = 25,       // 左膝
    RIGHT_KNEE = 26,      // 右膝
    LEFT_ANKLE = 27,      // 左脚踝
    RIGHT_ANKLE = 28,     // 右脚踝
    LEFT_HEEL = 29,       // 左脚跟
    RIGHT_HEEL = 30,      // 右脚跟
    LEFT_FOOT_INDEX = 31, // 左脚尖
    RIGHT_FOOT_INDEX = 32,// 右脚尖
    NONE = 40             // 无关键点
}
#endif
