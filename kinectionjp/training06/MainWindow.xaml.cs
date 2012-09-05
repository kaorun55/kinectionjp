using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

namespace training06
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
        /// 吹き出しのビットマップイメージ
        /// </summary>
        private BitmapImage fukidasiImage = null;

        /// <summary>
        /// 音源の角度
        /// </summary>
        private double soundDir = double.NaN;

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

                // 画像の読み込み
                maskImage = new BitmapImage( new Uri( @"pack://application:,,,/images/kaorun55.jpg" ) );
                fukidasiImage = new BitmapImage( new Uri( @"pack://application:,,,/images/fukidasi.png" ) );

                // RGB,Depth,Skeletonのイベントを受け取るイベントハンドラの登録
                kinect.AllFramesReady +=
                    new EventHandler<AllFramesReadyEventArgs>( kinect_AllFramesReady );

                // Kinectセンサーからのストリーム取得を開始
                kinect.Start();

                // 音声関連の設定
                kinect.AudioSource.SoundSourceAngleChanged += AudioSource_SoundSourceAngleChanged;
                kinect.AudioSource.Start();
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
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
        private List<Tuple<SkeletonPoint, Matrix4>> GetHeadPoints( SkeletonFrame skeletonFrame )
        {
            // 頭の位置のリストを作成
            List<Tuple<SkeletonPoint, Matrix4>> headPoints = new List<Tuple<SkeletonPoint, Matrix4>>();

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

                // 頭の位置と向きを保存する
                headPoints.Add( Tuple.Create( head.Position,
                    skeleton.BoneOrientations[JointType.Head].AbsoluteRotation.Matrix ) );
            }

            return headPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="colorFrame"></param>
        /// <param name="headList"></param>
        private void FillBitmap( ColorImageFrame colorFrame, DepthImageFrame depthFrame, List<Tuple<SkeletonPoint, Matrix4>> headList )
        {
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
                        int index = (depthColorPoint[i].Y * colorFrame.Width) + depthColorPoint[i].X;
                        depthMaskBuffer[index * 4 + 0] = 255;
                        depthMaskBuffer[index * 4 + 1] = 0;
                        depthMaskBuffer[index * 4 + 2] = 0;
                        depthMaskBuffer[index * 4 + 3] = 128;
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
                    ColorImagePoint headPoint = kinect.MapSkeletonPointToColor( head.Item1, rgbFormat );

                    // 頭の位置の向きに回転させたマスク画像を描画する
                    Matrix4 hm = head.Item2;
                    Matrix rot = new Matrix( -hm.M11, hm.M12,
                                             -hm.M21, hm.M22,
                                             headPoint.X, headPoint.Y );
                    drawContecxt.PushTransform( new MatrixTransform( rot ) );

                    // 距離に応じてサイズを決定する
                    int size = (int)(192 / head.Item1.Z);
                    drawContecxt.DrawImage( maskImage,
                        new Rect( -size / 2, -size / 2, size, size ) );
                    drawContecxt.Pop();

                    DrawFukidasi( drawContecxt, head.Item1, headPoint );
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
