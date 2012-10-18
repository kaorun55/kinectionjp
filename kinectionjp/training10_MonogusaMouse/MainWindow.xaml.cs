using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace training10_MonogusaMouse
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // 画像部分のサイズ
        private const int dirImgSize = 256;

        // [遊び]スライダーの値
        private double dirPlay
        {
            get
            {
                return sliderPlay.Value;
            }
        }

        // [倍率]スライダーの値
        private double moveAmp
        {
            get
            {
                return sliderAmp.Value;
            }
        }

        // KinectSensorChooser
        private KinectSensorChooser kinectChooser = new KinectSensorChooser();

        // Kinectセンサーからの骨格情報を受け取るバッファ
        private Skeleton[] skeletonBuffer = null;

        // 画面に表示するビットマップ
        private RenderTargetBitmap bmpBuffer = null;

        // ビットマップへの描画用DrawingVisual
        private DrawingVisual drawVisual = new DrawingVisual();

        public MainWindow()
        {
            InitializeComponent();
        }

        // 初期化処理(Kinectセンサーやバッファ類の初期化)
        private void WindowLoaded( object sender, RoutedEventArgs e )
        {
            rgbImage.Width = dirImgSize;
            rgbImage.Height = dirImgSize;

            // KinectSensorChooserの初期化
            kinectChooser.KinectChanged += KinectChanged;
            kinectChooser.Start();
        }

        // 終了処理
        private void WindowClosed( object sender, EventArgs e )
        {
            kinectChooser.Stop();
        }

        // Kinectセンサーの挿抜イベントに対し、初期化/終了処理を呼び出す
        private void KinectChanged( object sender, KinectChangedEventArgs args )
        {
            if ( args.OldSensor != null )
                UninitKinectSensor( args.OldSensor );

            if ( args.NewSensor != null )
                InitKinectSensor( args.NewSensor );
        }

        // Kinectセンサーの初期化
        private void InitKinectSensor( KinectSensor kinect )
        {
            // 骨格ストリームの有効化 (Near, Seated mode)
            SkeletonStream skelStream = kinect.SkeletonStream;
            kinect.DepthStream.Range = DepthRange.Near;
            skelStream.EnableTrackingInNearRange = true;
            skelStream.TrackingMode = SkeletonTrackingMode.Seated;
            skelStream.Enable();

            // バッファの初期化
            skeletonBuffer = new Skeleton[skelStream.FrameSkeletonArrayLength];
            bmpBuffer = new RenderTargetBitmap( dirImgSize, dirImgSize, 96, 96,
                                               PixelFormats.Default );
            rgbImage.Source = bmpBuffer;

            // イベントハンドラの登録
            kinect.AllFramesReady += AllFramesReady;
        }

        // Kinectセンサーの終了処理
        private void UninitKinectSensor( KinectSensor kinect )
        {
            kinect.AllFramesReady -= AllFramesReady;
        }

        // FrameReady イベントのハンドラ
        // 背景を描画し、骨格情報から頭の角度を取得しマウスを動かす
        private void AllFramesReady( object sender, AllFramesReadyEventArgs e )
        {
            // 描画の準備
            var drawCtx = drawVisual.RenderOpen();
            // 背景の描画
            drawBase( drawCtx );

            using ( SkeletonFrame skeletonFrame = e.OpenSkeletonFrame() ) {
                if ( skeletonFrame != null ) {
                    // 骨格情報をバッファにコピー
                    skeletonFrame.CopySkeletonDataTo( skeletonBuffer );

                    // 取得できた骨格毎にループ
                    foreach ( Skeleton skeleton in skeletonBuffer )
                        processSkeleton( skeleton, drawCtx );
                }
            }
            // 画面に表示するビットマップに描画
            drawCtx.Close();
            bmpBuffer.Render( drawVisual );
        }

        // 背景の描画
        private void drawBase( DrawingContext drawCtx )
        {
            drawCtx.DrawRectangle( Brushes.Black, null,
                                  new Rect( 0, 0, dirImgSize, dirImgSize ) );
            Pen axPen = new Pen( Brushes.Gray, 1 );
            drawCtx.DrawLine( axPen, new Point( 0, dirImgSize / 2 ),
                             new Point( dirImgSize, dirImgSize / 2 ) );
            drawCtx.DrawLine( axPen, new Point( dirImgSize / 2, 0 ),
                             new Point( dirImgSize / 2, dirImgSize ) );
            drawCtx.DrawRectangle( null, axPen,
                                  new Rect( (int)(dirImgSize * (0.5 - dirPlay)),
                                           (int)(dirImgSize * (0.5 - dirPlay)),
                                           (int)(dirImgSize * (2 * dirPlay)),
                                           (int)(dirImgSize * (2 * dirPlay)) ) );
        }

        // 骨格情報の処理(頭の向きを取得し、processHeadDirメソッドに渡す)
        private void processSkeleton( Skeleton skeleton, DrawingContext drawCtx )
        {
            // トラッキングできていない骨格は処理しない
            if ( skeleton.TrackingState != SkeletonTrackingState.Tracked )
                return;

            // 骨格から頭を取得
            Joint head = skeleton.Joints[JointType.Head];

            // 頭の位置が取得できない状態の場合は処理しない
            if ( head.TrackingState != JointTrackingState.Tracked
                && head.TrackingState != JointTrackingState.Inferred )
                return;

            // 頭の向きを取得
            Matrix4 headMtrx = (skeleton.BoneOrientations[JointType.Head]
                                .AbsoluteRotation.Matrix);
            processHeadDir( drawCtx, headMtrx );
        }

        // 頭の向きを受け取り、その方向にマウスを動かす
        private void processHeadDir( DrawingContext drawCtx, Matrix4 headMtrx )
        {
            bool isInvY = (checkBoxInvY.IsChecked.HasValue ?
                           checkBoxInvY.IsChecked.Value : false);
            double rawY = isInvY ? -headMtrx.M23 : headMtrx.M23;
            double rawX = headMtrx.M21;

            Point dirPt = new Point( dirImgSize * (1 + rawX) / 2,
                                    dirImgSize * (1 + rawY) / 2 );
            drawCtx.DrawEllipse( Brushes.Green, null, dirPt, 5, 5 );

            // 角度が小さい(遊び以下)の場合は0とみなす
            double dirX, dirY;
            if ( rawX > dirPlay )
                dirX = rawX - dirPlay;
            else if ( rawX < -dirPlay )
                dirX = rawX + dirPlay;
            else
                dirX = 0;
            if ( rawY > dirPlay )
                dirY = rawY - dirPlay;
            else if ( rawY < -dirPlay )
                dirY = rawY + dirPlay;
            else
                dirY = 0;

            // マウスを動かす
            NativeWrapper.sendMouseMove( (int)(dirX * moveAmp),
                                        (int)(dirY * moveAmp) );
        }
    }
}
