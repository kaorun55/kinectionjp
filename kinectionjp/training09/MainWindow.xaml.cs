﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace training09
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 制御するKinectセンサー
        /// </summary>
        KinectSensor kinect = null;

        /// <summary>
        /// Kinectセンサーからの画像情報を受けとるバッファ
        /// </summary>
        private byte[] pixelBuffer = null;

        /// <summary>
        /// 画面に表示するビットマップ
        /// </summary>
        private RenderTargetBitmap bmpBuffer = null;

        /// <summary>
        /// RGBカメラの解像度、フレームレート
        /// </summary>
        private const ColorImageFormat rgbFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// 距離カメラの解像度、フレームレート
        /// </summary>
        private const DepthImageFormat depthFormat = DepthImageFormat.Resolution640x480Fps30;

        /// <summary>
        /// Kinectセンサーから骨格情報を受け取るバッファ
        /// </summary>
        private Skeleton[] skeletonBuffer = null;

        /// <summary>
        /// 顔のビットマップイメージ
        /// </summary>
        private BitmapImage maskImage = null;

        /// <summary>
        /// 音源の角度
        /// </summary>
        private double soundDir = double.NaN;

        /// <summary>
        /// 吹き出しのビットマップイメージ
        /// </summary>
        private BitmapImage fukidasiImage = null;

        /// <summary>
        /// ビットマップへの描画用
        /// </summary>
        private DrawingVisual drawVisual = new DrawingVisual();

        /// <summary>
        /// Kinectセンサーからの深度情報を受け取るバッファ
        /// </summary>
        private short[] depthBuffer = null;

        /// <summary>
        /// 深度情報の各点に対する画像情報上の座標
        /// </summary>
        private ColorImagePoint[] depthColorPoint = null;

        /// <summary>
        /// 深度情報で背景を覆う画像のデータ
        /// </summary>
        private byte[] depthMaskBuffer = null;

        /// <summary>
        /// 音声認識エンジン
        /// </summary>
        private SpeechRecognitionEngine speechEngine = null;

        /// <summary>
        /// 認識したテキスト
        /// </summary>
        private string recognizedText = null;

        /// <summary>
        /// Kienctの挿抜管理
        /// </summary>
        private KinectSensorChooser kinectChooser = new KinectSensorChooser();

        /// <summary>
        /// ジェスチャー
        /// </summary>
        private PlayerGesture[] playerGesture;

        /// <summary>
        /// プレイヤーの情報
        /// </summary>
        class Player
        {
            public SkeletonPoint HeadPosition
            {
                set;
                get;
            }

            public Matrix4 HeadRotation
            {
                get;
                set;
            }

            public bool Posture
            {
                get;
                set;
            }
        }

        /// <summary>
        /// コンストラクター
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded( object sender, RoutedEventArgs e )
        {
            try {
                // 画像の読み込み
                maskImage = new BitmapImage( new Uri( @"pack://application:,,,/images/kaorun55.jpg" ) );
                fukidasiImage = new BitmapImage( new Uri( @"pack://application:,,,/images/fukidasi.png" ) );

                // 利用可能なKinectを探す
                kinect = KinectSensor.KinectSensors.First( k =>
                {
                    return k.Status == KinectStatus.Connected;
                } );

                // 利用可能なKinectがなかった
                if ( kinect == null ) {
                    throw new Exception( "Kinectが接続されていないか、準備ができていません" );
                }

                kinectChooser.KinectChanged += kinectChooser_KinectChanged;
                kinectChooser.PropertyChanged += kinectChooser_PropertyChanged;
                kinectChooser.Start();
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
        }

        void kinectChooser_KinectChanged( object sender, KinectChangedEventArgs e )
        {
            if ( e.OldSensor != null ) {
                StopKinect( e.OldSensor );
            }

            if ( e.NewSensor != null ) {
                StartKinect( e.NewSensor );
            }
        }

        void kinectChooser_PropertyChanged( object sender, System.ComponentModel.PropertyChangedEventArgs e )
        {
            if ( e.PropertyName.Equals( "Status" ) ) {
                textBlockStatus.Text = "Status:" + kinectChooser.Status;
            }
        }

        private void StartKinect( KinectSensor k )
        {
            kinect = k;

            // ストリームの有効化
            kinect.ColorStream.Enable( rgbFormat );
            kinect.DepthStream.Enable( depthFormat );

            // RGBカメラ用バッファの初期化
            pixelBuffer = new byte[kinect.ColorStream.FramePixelDataLength];
            bmpBuffer = new RenderTargetBitmap( kinect.ColorStream.FrameWidth,
                kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Default );

            rgbImage.Source = bmpBuffer;

            // 距離カメラ用バッファの初期化
            depthBuffer = new short[kinect.DepthStream.FramePixelDataLength];
            depthColorPoint = new ColorImagePoint[kinect.DepthStream.FramePixelDataLength];
            depthMaskBuffer = new byte[kinect.ColorStream.FramePixelDataLength];

            // 骨格ストリームの有効化
            kinect.SkeletonStream.Enable();

            // 骨格ストリーム用のバッファの初期化
            skeletonBuffer = new Skeleton[kinect.SkeletonStream.FrameSkeletonArrayLength];
            playerGesture = new PlayerGesture[kinect.SkeletonStream.FrameSkeletonArrayLength];
            for ( int i = 0; i < playerGesture.Length; i++ ) {
                playerGesture[i] = new PlayerGesture();
            }

            // RGB,Depth,Skeletonのイベントを受け取るイベントハンドラの登録
            kinect.AllFramesReady +=
                    new EventHandler<AllFramesReadyEventArgs>( kinect_AllFramesReady );

            // Kinectセンサーからのストリーム取得を開始
            // KinectSensorChooserでやってくれる
            //kinect.Start();


            // 音声認識関連の設定
            kinect.AudioSource.SoundSourceAngleChanged += AudioSource_SoundSourceAngleChanged;
            var stream = kinect.AudioSource.Start();

            speechEngine = InitSpeechEngine();
            speechEngine.SpeechRecognized += speechEngine_SpeechRecognized;
            var format = new SpeechAudioFormatInfo( EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null );
            speechEngine.SetInputToAudioStream( stream, format );
            speechEngine.RecognizeAsync( RecognizeMode.Multiple );
        }

        /// <summary>
        /// 音源の方向が変わったことが通知される
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AudioSource_SoundSourceAngleChanged( object sender, SoundSourceAngleChangedEventArgs e )
        {
            soundDir = e.ConfidenceLevel > 0.5 ? e.Angle : double.NaN;
        }

        /// <summary>
        /// 音声認識エンジンの初期化
        /// </summary>
        /// <returns></returns>
        private SpeechRecognitionEngine InitSpeechEngine()
        {
            RecognizerInfo target = SpeechRecognitionEngine.InstalledRecognizers()
                .Where( ( r ) =>
                {
                    return r.AdditionalInfo.ContainsKey( "Kinect" ) && 
                           r.AdditionalInfo["Kinect"].Equals( "True", StringComparison.OrdinalIgnoreCase ) &&
                           r.Culture.Name.Equals( "ja-JP", StringComparison.OrdinalIgnoreCase );
                } )
                .FirstOrDefault();
            if ( target == null ) {
                throw new Exception( "Kinect用の認識エンジンがありません" );
            }

            // 認識する単語の追加
            var words = new Choices();
            words.Add( "キネクト" );
            words.Add( "テスト" );

            // 音声認識エンジンの作成
            var grammarBuilder = new GrammarBuilder();
            grammarBuilder.Culture = target.Culture;
            grammarBuilder.Append( words );

            var engine = new SpeechRecognitionEngine( target.Id );
            engine.LoadGrammar( new Grammar( grammarBuilder ) );

            return engine;
        }

        /// <summary>
        /// 音声認識結果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void speechEngine_SpeechRecognized( object sender, SpeechRecognizedEventArgs e )
        {
            if ( (e.Result) != null && (e.Result.Confidence >= 0/3) ) {
                recognizedText = e.Result.Text;
            }
            else {
                recognizedText = null;
            }
        }

        /// <summary>
        /// RGB,Depth,Skeletonの更新通知
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void kinect_AllFramesReady( object sender, AllFramesReadyEventArgs e )
        {
            using ( SkeletonFrame skeletonFrame = e.OpenSkeletonFrame() ) {
                using ( ColorImageFrame colorFrame = e.OpenColorImageFrame() ) {
                    using ( DepthImageFrame depthFrame = e.OpenDepthImageFrame() ) {
                        if ( (skeletonFrame != null) && (colorFrame != null) && (depthFrame != null) ) {
                            FillBitmap( colorFrame, depthFrame, GetHeadPoints( skeletonFrame ) );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 頭の位置を返す
        /// </summary>
        /// <param name="skeletonFrame"></param>
        /// <returns></returns>
        private List<Player> GetHeadPoints( SkeletonFrame skeletonFrame )
        {
            // 頭の位置のリストを作成
            List<Player> headPoints = new List<Player>();

            // 骨格情報をバッファにコピー
            skeletonFrame.CopySkeletonDataTo( skeletonBuffer );

            // 取得できた骨格ごとにループ
            for ( int i = 0; i < skeletonBuffer.Length; i++ ) {
                Skeleton skeleton = skeletonBuffer[i];
                // トラッキングできていない骨格は処理しない
                if ( skeleton.TrackingState != SkeletonTrackingState.Tracked ) {
                    continue;
                }

                // 頭の位置を取得する
                Joint head = skeleton.Joints[JointType.Head];

                // 頭の位置をトラッキングしていない場合は処理しない
                if ( head.TrackingState == JointTrackingState.NotTracked ) {
                    continue;
                }

                // 頭の位置と向きを保存する
                headPoints.Add( new Player() {
                    HeadPosition = head.Position,
                    HeadRotation = skeleton.BoneOrientations[JointType.Head].AbsoluteRotation.Matrix,
                    Posture = playerGesture[i].Update( skeleton ),
                } );
            }

            return headPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="colorFrame"></param>
        /// <param name="headList"></param>
        private void FillBitmap( ColorImageFrame colorFrame, DepthImageFrame depthFrame, List<Player> headList )
        {
            if ( kinect == null ) {
                return;
            }

            // 描画の準備
            using ( var drawContecxt = drawVisual.RenderOpen() ) {
                // 画像情報をバッファにコピー
                colorFrame.CopyPixelDataTo( pixelBuffer );
                depthFrame.CopyPixelDataTo( depthBuffer );

                // 深度情報の各店に対する画像情報上の座標を取得
                kinect.MapDepthFrameToColorFrame( depthFormat, depthBuffer, rgbFormat, depthColorPoint );

                // 深度情報の各点を調べ、プレイヤーがいない場合は薄い青を塗る
                Array.Clear( depthMaskBuffer, 0, depthMaskBuffer.Length );
                for ( int i = 0; i < depthBuffer.Length; i++ ) {
                    if ( (depthBuffer[i] & DepthImageFrame.PlayerIndexBitmask) == 0 ) {
                        int index = ((depthColorPoint[i].Y * colorFrame.Width) + depthColorPoint[i].X) * 4;
                        depthMaskBuffer[index + 0] = 255;
                        //depthMaskBuffer[index + 1] = 0;
                        //depthMaskBuffer[index + 2] = 0;
                        depthMaskBuffer[index + 3] = 128;
                    }
                }

                var rect = new Int32Rect( 0, 0, colorFrame.Width, colorFrame.Height );
                var rect2 = new Rect( 0, 0, colorFrame.Width, colorFrame.Height );
                var mask = new WriteableBitmap( colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgra32, null );
                mask.WritePixels( rect, depthMaskBuffer, colorFrame.Width * 4, 0 );

                // カメラの画像情報から、背景のビットマップを作成し描画する
                var backgroundImage = new WriteableBitmap( colorFrame.Width, colorFrame.Height, 96, 96,
                    PixelFormats.Bgr32, null );
                backgroundImage.WritePixels( rect, pixelBuffer, colorFrame.Width * 4, 0 );
                drawContecxt.DrawImage( backgroundImage, rect2 );
                drawContecxt.DrawImage( mask, rect2 );

                // 頭の位置にマスク画像を表示する
                foreach ( var head in headList ) {
                    ColorImagePoint headPoint = kinect.MapSkeletonPointToColor( head.HeadPosition, rgbFormat );

                    // 頭の位置の向きに回転させたマスク画像を描画する
                    Matrix4 hm = head.HeadRotation;
                    Matrix rot = new Matrix( -hm.M11, hm.M12,
                                             -hm.M21, hm.M22,
                                             headPoint.X, headPoint.Y );
                    drawContecxt.PushTransform( new MatrixTransform( rot ) );

                    // 距離に応じてサイズを決定する
                    int size = (int)(192 / head.HeadPosition.Z);
                    if ( head.Posture ) {
                        size *= 2;
                    }

                    drawContecxt.DrawImage( maskImage,
                        new Rect( -size / 2, -size / 2, size, size ) );
                    drawContecxt.Pop();

                    DrawFukidasi( drawContecxt, head.HeadPosition, headPoint );
                }
            }

            // 画面に表示するビットマップに描画する
            bmpBuffer.Render( drawVisual );
        }

        /// <summary>
        /// 吹き出しを描画する
        /// </summary>
        /// <param name="drawContecxt"></param>
        /// <param name="head"></param>
        /// <param name="headPoint"></param>
        /// <returns></returns>
        private void DrawFukidasi( DrawingContext drawContecxt, SkeletonPoint head, ColorImagePoint headPoint )
        {
            double angle = Math.Atan2( head.X, head.Z ) * 180 / Math.PI;
            if ( Math.Abs( soundDir - angle ) < 10 ) {
                Rect rect = new Rect( headPoint.X + 32, headPoint.Y - 64, 96, 64 );
                drawContecxt.DrawImage( fukidasiImage, rect );

                if ( recognizedText != null ) {
                    var text = new FormattedText( recognizedText,
                        CultureInfo.GetCultureInfo( "ja-JP" ),
                        FlowDirection.LeftToRight,
                        new Typeface( "Verdana" ),
                        24, Brushes.Black );
                    drawContecxt.DrawText( text, new Point( head.X + 56, head.Y - 48 ) );
                }
            }
        }

        /// <summary>
        /// 終了処理(ウィンドウが閉じられるとき)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
        {
            try {
                kinectChooser.Stop();
                StopKinect( kinect );
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
        }

        private void StopKinect( KinectSensor k )
        {
            // Kinectを動かしていたら止める
            if ( k != null ) {
                // ストリームの停止とリソースの解放
                k.Stop();

                // イベントハンドラの削除
                k.AllFramesReady -= kinect_AllFramesReady;

                kinect = null;
            }
        }

    }

    /// <summary>
    /// プレーヤーごとのジェスチャーデータ
    /// </summary>
    class PlayerGesture
    {
        private int State;

        private Skeleton PrevSkeelton;

        public bool Update( Skeleton skeleton )
        {
            bool gesture = false;

            int nextState = 0;
            if ( checkTransState( skeleton, State, PrevSkeelton ) ) {
                nextState = State;
            }

            int posture = checkGesturePosure( skeleton );
            if ( (State == 0) && (posture == 1) ) {
                nextState = 1;
            }
            else if ( (State == 1) && (posture == 2) ) {
                nextState = 2;
            }
            else if ( (State == 2) && (posture == 3) ) {
                nextState = 3;
            }
            else if ( (State == 3) && (posture == 4) ) {
                nextState = 4;
            }
            else if ( (State == 4) && (posture == 1) ) {
                nextState = 5;
            }
            else if ( (State == 5) && (posture == 1) ) {
                gesture = true;
                nextState = 5;
            }

            Trace.WriteLine( nextState.ToString() );
            State = nextState;
            PrevSkeelton = skeleton;

            return gesture;
        }

        private int checkGesturePosure( Skeleton skeleton )
        {
            var head = skeleton.Joints[JointType.Head];
            var rwrist = skeleton.Joints[JointType.WristRight];
            var center = skeleton.Joints[JointType.HipCenter];

            if ( !IsTracked( head ) || !IsTracked( rwrist ) || !IsTracked( center ) ) {
                return 0;
            }

            if ( (head.Position.Y < rwrist.Position.Y) && (head.Position.X < rwrist.Position.X) ) {
                return 1;
            }
            else if ( (rwrist.Position.Y < center.Position.Y) && (center.Position.X < rwrist.Position.X) ) {
                return 2;
            }
            else if ( (rwrist.Position.Y < center.Position.Y) && (rwrist.Position.X < center.Position.X) ) {
                return 3;
            }
            else if ( (head.Position.Y < rwrist.Position.Y) && (rwrist.Position.X < head.Position.X) ) {
                return 4;
            }

            return 0;
        }

        private const double MOVE_PLAY = 0.1;
        private bool checkTransState( Skeleton skeleton, int state, Skeleton prevSkeleton )
        {
            if ( prevSkeleton == null ) {
                return false;
            }

            // 必要な関節、方向を取得
            var head = skeleton.Joints[JointType.Head];
            var rwrist = skeleton.Joints[JointType.WristRight];
            var center = skeleton.Joints[JointType.HipCenter];
            var prevrw = prevSkeleton.Joints[JointType.WristRight];

            if ( !IsTracked( head ) || !IsTracked( rwrist ) || !IsTracked( center ) || !IsTracked( prevrw ) ) {
                return false;
            }

            if ( (state == 1) && (center.Position.X < rwrist.Position.X) && (rwrist.Position.Y < prevrw.Position.Y + MOVE_PLAY) ) {
                return true;
            }
            else if ( (state == 2) && (rwrist.Position.Y < center.Position.Y) && (rwrist.Position.X < prevrw.Position.X + MOVE_PLAY) ) {
                return true;
            }
            else if ( (state == 3) && (rwrist.Position.X < center.Position.X) && (prevrw.Position.Y < rwrist.Position.Y + MOVE_PLAY) ) {
                return true;
            }
            else if ( (state == 4) && (head.Position.Y < rwrist.Position.Y) && (prevrw.Position.X < rwrist.Position.X + MOVE_PLAY) ) {
                return true;
            }

            return false;
        }

        private static bool IsTracked( Joint joint )
        {
            return joint.TrackingState != JointTrackingState.NotTracked;
        }
    }
}
