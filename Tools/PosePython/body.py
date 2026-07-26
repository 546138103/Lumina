# MediaPipe Body
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
from clientUDP import ClientUDP
from preview_client import PreviewClient

import cv2
import threading
import time
import global_vars 
import struct

# the capture thread captures images from the WebCam on a separate thread (for performance)
class CaptureThread(threading.Thread):
    cap = None
    ret = None
    frame = None
    isRunning = False
    counter = 0
    timer = 0.0
    def run(self):
        self.cap = cv2.VideoCapture(global_vars.CAM_INDEX) # sometimes it can take a while for certain video captures

        time.sleep(1)
        
        print("Opened Capture @ %s fps, %sx%s" % (
            str(self.cap.get(cv2.CAP_PROP_FPS)),
            str(int(self.cap.get(cv2.CAP_PROP_FRAME_WIDTH))),
            str(int(self.cap.get(cv2.CAP_PROP_FRAME_HEIGHT)))))
        while not global_vars.KILL_THREADS:
            self.ret, self.frame = self.cap.read()
            self.isRunning = True
            if global_vars.DEBUG:
                self.counter = self.counter+1
                if time.time()-self.timer>=3:
                    print("Capture FPS: ",self.counter/(time.time()-self.timer))
                    self.counter = 0
                    self.timer = time.time()

# the body thread actually does the 
# processing of the captured images, and communication with unity
class BodyThread(threading.Thread):
    data = ""
    dirty = True
    pipe = None
    timeSinceCheckedConnection = 0
    timeSincePostStatistics = 0

    def run(self):
        mp_drawing = mp.solutions.drawing_utils
        mp_pose = mp.solutions.pose

        self.setup_comms()

        preview_client = None
        if global_vars.SEND_UNITY_PREVIEW:
            preview_client = PreviewClient(
                global_vars.PREVIEW_HOST,
                global_vars.PREVIEW_PORT,
                global_vars.PREVIEW_JPEG_QUALITY,
                global_vars.PREVIEW_FPS,
            )
            preview_client.start()
        
        capture = CaptureThread()
        capture.start()

        with mp_pose.Pose(min_detection_confidence=0.80, min_tracking_confidence=0.5, model_complexity = global_vars.MODEL_COMPLEXITY,static_image_mode = False,enable_segmentation = True) as pose: 
            
            while not global_vars.KILL_THREADS and capture.isRunning==False:
                print("Waiting for camera and capture thread.")
                time.sleep(0.5)
            print("Beginning capture")

            if global_vars.SHOW_OPENCV_WINDOW:
                # Keep the preview at the camera frame's native aspect ratio.
                cv2.namedWindow('Body Tracking', cv2.WINDOW_AUTOSIZE)
                 
            while not global_vars.KILL_THREADS and capture.cap.isOpened():
                ti = time.time()

                # Fetch stuff from the capture thread
                ret = capture.ret
                image = capture.frame

                if not ret or image is None:
                    time.sleep(0.005)
                    continue
                                
                # Image transformations and stuff
                image = resize_to_fit(
                    image,
                    global_vars.PROCESS_MAX_WIDTH,
                    global_vars.PROCESS_MAX_HEIGHT)
                image = cv2.flip(image, 1)
                image.flags.writeable = False
                
                # Detections
                results = pose.process(image)
                image.flags.writeable = True
                tf = time.time()
                
                # Diagnostic timing is independent from preview rendering.
                if global_vars.DEBUG:
                    if time.time()-self.timeSincePostStatistics>=1:
                        print("Theoretical Maximum FPS: %f"%(1/(tf-ti)))
                        self.timeSincePostStatistics = time.time()

                # Render one annotated frame for either Unity or the optional
                # OpenCV window. The landmark transport remains independent.
                should_render_preview = (
                    global_vars.SEND_UNITY_PREVIEW
                    or global_vars.SHOW_OPENCV_WINDOW
                )
                if should_render_preview:
                    preview_image = image.copy()

                    if results.pose_landmarks:
                        if global_vars.DRAW_PREVIEW_LANDMARKS:
                            mp_drawing.draw_landmarks(
                                preview_image,
                                results.pose_landmarks,
                                mp_pose.POSE_CONNECTIONS,
                                mp_drawing.DrawingSpec(
                                    color=(255, 100, 0),
                                    thickness=2,
                                    circle_radius=4,
                                ),
                                mp_drawing.DrawingSpec(
                                    color=(255, 255, 255),
                                    thickness=2,
                                    circle_radius=2,
                                ),
                            )

                    if preview_client is not None:
                        preview_client.submit_frame(preview_image)

                    if global_vars.SHOW_OPENCV_WINDOW:
                        cv2.imshow('Body Tracking', preview_image)
                        cv2.waitKey(3)

                # Set up data for relay
                self.data = ""
                i = 0
                if results.pose_world_landmarks:
                    hand_world_landmarks = results.pose_world_landmarks
                    for i in range(0,33):
                        self.data += "{}|{}|{}|{}\n".format(i,hand_world_landmarks.landmark[i].x,hand_world_landmarks.landmark[i].y,hand_world_landmarks.landmark[i].z)

                self.send_data(self.data)
                    
        if self.pipe is not None:
            self.pipe.close()
        if preview_client is not None:
            preview_client.stop()
            preview_client.join(timeout=2.0)
        if capture.cap is not None:
            capture.cap.release()
        cv2.destroyAllWindows()
        pass

    def setup_comms(self):
        if not global_vars.USE_LEGACY_PIPES:
            self.client = ClientUDP(global_vars.HOST,global_vars.PORT)
            self.client.start()
        else:
            print("Using Pipes for interprocess communication (not supported on OSX or Linux).")
        pass
    def send_data(self,message):
        if not global_vars.USE_LEGACY_PIPES:
            self.client.sendMessage(message)
            pass
        else:
            # Maintain pipe connection.
            if self.pipe==None and time.time()-self.timeSinceCheckedConnection>=1:
                try:
                    self.pipe = open(r'\\.\pipe\UnityMediaPipeBody1', 'r+b', 0)
                except FileNotFoundError:
                    print("Waiting for Unity project to run...")
                    self.pipe = None
                self.timeSinceCheckedConnection = time.time()

            if self.pipe != None:
                try:     
                    s = self.data.encode('utf-8') 
                    self.pipe.write(struct.pack('I', len(s)) + s)   
                    self.pipe.seek(0)    
                except Exception as ex:  
                    print("Failed to write to pipe. Is the unity project open?")
                    self.pipe= None
        pass


def resize_to_fit(image, max_width, max_height):
    """Resize without changing the camera aspect ratio or upscaling."""
    source_height, source_width = image.shape[:2]
    if source_width <= 0 or source_height <= 0:
        return image

    scale = min(
        max_width / source_width,
        max_height / source_height,
        1.0,
    )

    if scale >= 1.0:
        return image

    target_width = max(1, int(round(source_width * scale)))
    target_height = max(1, int(round(source_height * scale)))
    return cv2.resize(
        image,
        (target_width, target_height),
        interpolation=cv2.INTER_AREA)
