using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Collections.Generic;

namespace training04
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
        const ColorImageFormat rgbFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Kinectセンサーから骨格情報を受け取るバッファ
        /// </summary>
        private Skeleton[] skeletonBuffer = null;

        /// <summary>
        /// 顔のビットマップイメージ
        /// </summary>
        private BitmapImage maskImage = null;

        /// <summary>
        /// ビットマップへの描画用
        /// </summary>
        private DrawingVisual drawVisual = new DrawingVisual();

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
                // 利用可能なKinectを探す
                kinect = KinectSensor.KinectSensors.First( k =>
                {
                    return k.Status == KinectStatus.Connected;
                } );

                // 利用可能なKinectがなかった
                if ( kinect == null ) {
                    throw new Exception( "Kinectが接続されていないか、準備ができていません" );
                }

                // カラーストリームの有効化
                kinect.ColorStream.Enable( rgbFormat );

                // カラーストリーム用バッファの初期化
                pixelBuffer = new byte[kinect.ColorStream.FramePixelDataLength];
                bmpBuffer = new RenderTargetBitmap( kinect.ColorStream.FrameWidth,
                    kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Default );

                rgbImage.Source = bmpBuffer;

                // 骨格ストリームの有効化
                kinect.SkeletonStream.Enable();

                // 骨格ストリーム用のバッファの初期化
                skeletonBuffer = new Skeleton[kinect.SkeletonStream.FrameSkeletonArrayLength];

                // 画像の読み込み
                maskImage = new BitmapImage( new Uri(@"pack://application:,,,/images/kaorun55.jpg") );

                // RGB,Depth,Skeletonのイベントを受け取るイベントハンドラの登録
                kinect.AllFramesReady +=
                    new EventHandler<AllFramesReadyEventArgs>( kinect_AllFramesReady );

                // Kinectセンサーからのストリーム取得を開始
                kinect.Start();
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
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
                    if ( (skeletonFrame != null) && (colorFrame != null) ) {
                        FillBitmap( colorFrame, GetHeadPoints( skeletonFrame ) );
                    }
                }
            }
        }

        /// <summary>
        /// 頭の位置を返す
        /// </summary>
        /// <param name="skeletonFrame"></param>
        /// <returns></returns>
        private List<SkeletonPoint> GetHeadPoints( SkeletonFrame skeletonFrame )
        {
            // 頭の位置のリストを作成
            List<SkeletonPoint> headPoints = new List<SkeletonPoint>();

            // 骨格情報をバッファにコピー
            skeletonFrame.CopySkeletonDataTo( skeletonBuffer );

            // 取得できた骨格ごとにループ
            foreach ( Skeleton skeleton in skeletonBuffer ) {
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

                // 頭の位置を保存する
                headPoints.Add( head.Position );
            }

            return headPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="colorFrame"></param>
        /// <param name="headList"></param>
        private void FillBitmap( ColorImageFrame colorFrame, List<SkeletonPoint> headList )
        {
            // 描画の準備
            using ( var drawContecxt = drawVisual.RenderOpen() ) {
                // 画像情報をバッファにコピー
                colorFrame.CopyPixelDataTo( pixelBuffer );

                // カメラの画像情報から、背景のビットマップを作成し描画する
                var backgroundImage = new WriteableBitmap( colorFrame.Width, colorFrame.Height, 96, 96,
                    PixelFormats.Bgr32, null );
                backgroundImage.WritePixels( new Int32Rect( 0, 0, colorFrame.Width, colorFrame.Height ),
                    pixelBuffer, colorFrame.Width * 4, 0 );
                drawContecxt.DrawImage( backgroundImage, new Rect( 0, 0, colorFrame.Width, colorFrame.Height ) );

                // 頭の位置にマスク画像を表示する
                foreach ( SkeletonPoint head in headList ) {
                    ColorImagePoint headPoint = kinect.MapSkeletonPointToColor( head, rgbFormat );
                    drawContecxt.DrawImage( maskImage, new Rect( headPoint.X - 64, headPoint.Y - 64, 128, 128 ) );
                }
            }

            // 画面に表示するビットマップに描画する
            bmpBuffer.Render( drawVisual );
        }

        /// <summary>
        /// 終了処理(ウィンドウが閉じられるとき)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
        {
            try {
                // Kinectを動かしていたら止める
                if ( kinect != null ) {
                    // イベントハンドラの削除
                    kinect.AllFramesReady -= kinect_AllFramesReady;

                    // ストリームの停止とリソースの解放
                    kinect.Stop();
                    kinect.Dispose();

                    kinect = null;
                }
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
        }
    }
}
