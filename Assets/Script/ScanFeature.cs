using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
public class ScanFeature : ScriptableRendererFeature {
	//创建一个setting，用来从外部输入材质和参数
	[System.Serializable]
	public class Settings {
		public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingTransparents;
		[FormerlySerializedAs( "scanShader" )]
		public Material scanMaterial;

		[Header( "Static Settings" )]
		public Color scanColorHead = Color.blue;
		public Color scanColor = Color.blue;
		public float outlineWidth = 0.1f;
		public float scanLineWidth = 1f;
		public float scanLineInterval = 1f;
		public float headScanLineWidth = 1f;

		[Header( "Dynamics Settings(control by code)" )]
		public float scanLineBrightness = 1f;
		public float scanRange = 1f;
		public float outlineBrightness = 1f;
		public float headScanLineDistance = 8f;
		public Vector3 scanCenterWS = new Vector3( 123.05f, 36.3f, 147.86f );
		public float outlineStarDistance = 30f;

		[Header( "Render Mark" )]
		public Material markMaterial;
		public GameObject markParticle3;
		public GameObject markParticle2;
		public GameObject markParticle1;
	}

	public Settings settings = new Settings();

	static ScanFeature _instance;

	//新建一个CustomRenderPass
	CustomRenderPass _myPass;

	readonly static int ScanColorHead = Shader.PropertyToID( "scanColorHead" );
	readonly static int ScanColor = Shader.PropertyToID( "scanColor" );

	readonly static int OutlineWidth = Shader.PropertyToID( "outlineWidth" );
	readonly static int OutlineBrightness = Shader.PropertyToID( "outlineBrightness" );
	readonly static int OutlineStarDistance = Shader.PropertyToID( "outlineStarDistance" );

	readonly static int ScanLineWidth = Shader.PropertyToID( "scanLineWidth" );
	readonly static int ScanLineInterval = Shader.PropertyToID( "scanLineInterval" );
	readonly static int ScanLineBrightness = Shader.PropertyToID( "scanLineBrightness" );
	readonly static int ScanRange = Shader.PropertyToID( "scanRange" );

	readonly static int HeadScanLineDistance = Shader.PropertyToID( "headScanLineDistance" );
	readonly static int HeadScanLineWidth = Shader.PropertyToID( "headScanLineWidth" );
	readonly static int HeadScanLineBrightness = Shader.PropertyToID( "headScanLineBrightness" );
	readonly static int ScanCenterWs = Shader.PropertyToID( "scanCenterWS" );

	// 地形标记的参数
	readonly static int ColorAlpha = Shader.PropertyToID( "colorAlpha" );


	static bool canScan = true;
	static bool showMark = false;
	static Tween markTween;

	public static void ExecuteScan( Transform player ) {
		StartScan( player ).Forget();
	}

	static async UniTaskVoid StartScan( Transform player ) {
		if( !canScan ) {
			return;
		}
		canScan = false;
		showMark = true;

		// 万一上一个mark还没消失，手动取消
		markTween?.Kill();
		var scanCenter = player.position - player.forward * 2;

		var material = _instance.settings.scanMaterial;
		var markMaterial = _instance.settings.markMaterial;
		material.SetVector( ScanCenterWs, scanCenter );

		// 控制扫描线前进
		material.SetFloat( HeadScanLineDistance, 4 );
		material.DOFloat( 250, HeadScanLineDistance, 3.5f ).SetEase( Ease.InSine ).onComplete += () => {
			canScan = true;
		};

		// 随着距离前进，扫描范围变大
		material.SetFloat( ScanRange, 1 );
		material.DOFloat( 5, ScanRange, 1.5f ).SetEase( Ease.InSine ).SetDelay( 1 );

		// 控制扫描线和最前方的扫描线颜色颜色
		material.SetFloat( ScanLineBrightness, 0.3f );
		material.SetFloat( HeadScanLineBrightness, 0 );
		material.DOFloat( 1, ScanLineBrightness, 0.2f ).SetDelay( 0.25f );
		material.DOFloat( 1, HeadScanLineBrightness, 0.1f ).SetDelay( 0.25f );
		material.DOFloat( 0, ScanLineBrightness, 0.5f ).SetDelay( 2.25f ).SetEase( Ease.Linear );
		material.DOFloat( 0, HeadScanLineBrightness, 0.5f ).SetDelay( 2.25f ).SetEase( Ease.Linear );

		// 控制轮廓
		material.SetFloat( OutlineBrightness, 1 );
		material.SetFloat( OutlineStarDistance, 0 );
		material.DOFloat( 0, OutlineBrightness, 0.5f ).SetDelay( 2.25f ).SetEase( Ease.Linear );
		material.DOFloat( 30, OutlineStarDistance, 1f ).SetEase( Ease.InCubic );

		// 控制地形标记的透明度
		markMaterial.SetFloat( ColorAlpha, 0 );
		markMaterial.DOFloat( 1, ColorAlpha, 1f );
		markTween = markMaterial.DOFloat( 0, ColorAlpha, 1f ).SetDelay( 7 );
		markTween.onComplete += () => {
			showMark = false;
		};

		//生成地形标记
		await GenerateTerrainMarks( player );
	}


	static ProfilerMarker _generateTerrainMarks = new ProfilerMarker( "GenerateTerrainMarks" );
	struct Marks {
		public Vector3 markPosition;
		public int markCategory;
	}
	static Marks[] _marks; // 存每个标记的数据
	const int horizentalCount = 70; // 横向的列数
	const int verticalCount = 50; // 向前的点行数
	const float gridStep = 0.5f; // 两个点之间的距离

	static void ShootParticle( Vector3 position, Vector3 normal, int index = 3 ) {
		float distanceToCamera01 = Vector3.Distance( position, Camera.main.transform.position ) / 20 + 0.5f;

		GameObject instance;
		switch( index ) {
			case 3:
				instance = Instantiate( _instance.settings.markParticle3 );
				break;
			case 2:
				instance = Instantiate( _instance.settings.markParticle2 );
				break;
			default:
				instance = Instantiate( _instance.settings.markParticle1 );
				break;
		}
		instance.transform.position = position;
		instance.transform.localScale = Random.Range( 0.5f, 1.5f ) * Vector3.one * distanceToCamera01;
		instance.transform.GetChild( 0 ).localScale = Random.Range( 2f, 5f ) * Vector3.one * distanceToCamera01;
	}

	static async UniTask GenerateTerrainMarks( Transform player ) {
		// 每次扫描前清空数组
		Array.Clear( _marks, 0, _marks.Length );
		var forward = player.forward;
		var right = player.right;


		// 把撒点的初始位置顶到角色头顶的左后方
		Vector3 position = player.position - forward * 2 + Vector3.up * 100;
		var rayCastPos = position - right * horizentalCount / 2 * gridStep - forward * ( 3 * gridStep );

		// 横向纵向套两个循环，不断碰撞检测和写入数组
		for( int i = 0; i < verticalCount; i++ ) {
			_generateTerrainMarks.Begin();
			for( int j = 0; j < horizentalCount; j++ ) {
				Physics.Raycast( rayCastPos, Vector3.down, out RaycastHit hit, 300, LayerMask.GetMask( "Scan", "Road" ) );
				if( hit.collider is null ) {
					rayCastPos += right * gridStep;
					continue;
				}
				var normal = hit.normal;

				// 根据法线的纵向值来判断斜率，设置该点的标志是什么
				if( hit.collider.isTrigger ) {
					Physics.Raycast( rayCastPos, Vector3.down, out hit, 300, LayerMask.GetMask( "Scan" ) );
					_marks[i * horizentalCount + j].markCategory = 0;
					_marks[i * horizentalCount + j].markPosition = hit.point;
				} else if( normal.y < 0.75f ) {
					_marks[i * horizentalCount + j].markCategory = 3;
					// 红叉只有33%的概率出现
					if( Random.Range( 0f, 1f ) < 0.3f ) {
						_marks[i * horizentalCount + j].markPosition = hit.point;
						ShootParticle( hit.point, normal, 3 );
					}
				} else if( normal.y < 0.85f ) {
					_marks[i * horizentalCount + j].markCategory = 2;
					_marks[i * horizentalCount + j].markPosition = hit.point;
					if( Random.Range( 0f, 1f ) < 0.0003 ) {
						ShootParticle( hit.point, normal, 1 );
					}
				} else {
					_marks[i * horizentalCount + j].markCategory = 1;
					_marks[i * horizentalCount + j].markPosition = hit.point;
					if( Random.Range( 0f, 1f ) < 0.0002 ) {
						ShootParticle( hit.point, normal, 1 );
					}
				}

				rayCastPos += right * gridStep;

				// debug 显示绘制
				// if( hit.normal.y < 0.8f ) {
				// 	Debug.DrawLine( hit.point, hit.point + hit.normal * 0.2f, Color.red, 10 );
				// } else if( hit.normal.y < 0.9f ) {
				// 	Debug.DrawLine( hit.point, hit.point + hit.normal * 0.2f, Color.yellow, 10 );
				// } else {
				// 	Debug.DrawLine( hit.point, hit.point + hit.normal * 0.2f, Color.cyan, 10 );
				// }
			}
			_generateTerrainMarks.End();

			rayCastPos -= right * horizentalCount * gridStep;
			rayCastPos += forward * gridStep;
			
			//每次生成一行地形标记后，等待一帧，并绘制当前帧的地形标记
			await UniTask.Yield();

		
		}
	}


	/// <summary>
	/// 这里是自定义的渲染pass
	/// </summary>
	class CustomRenderPass : ScriptableRenderPass {
		//创建RTHandle,用来存储相机的颜色和深度缓冲区
		RTHandle _cameraColor;
		RTHandle _cameraDepth;
		RTHandle _cameraNormal;
		RTHandle _tempTex;
		//纹理描述器
		RenderTextureDescriptor m_Descriptor;
		//cmd name
		string _passName;
		Settings settings;

		GraphicsBuffer _graphicsBuffer;
		GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
		ComputeBuffer _computeBuffer;
		//初始类的时候传入材质

		Mesh mesh;
		public CustomRenderPass( Settings settings ) {
			_graphicsBuffer = new GraphicsBuffer( GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size );
			_commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
			_computeBuffer = new ComputeBuffer( horizentalCount * verticalCount, sizeof( float ) * 4 );

			mesh = new Mesh{
				vertices = new Vector3[6],
				uv = new[]{
					new Vector2( 0, 0 ),
					new Vector2( 1, 1 ),
					new Vector2( 0, 1 ),
					new Vector2( 0, 0 ),
					new Vector2( 1, 0 ),
					new Vector2( 1, 1 ),
				}
			};

			var scanMaterial = settings.scanMaterial;

			scanMaterial.SetColor( ScanColorHead, settings.scanColorHead );
			scanMaterial.SetColor( ScanColor, settings.scanColor );
			scanMaterial.SetFloat( OutlineWidth, settings.outlineWidth );
			scanMaterial.SetFloat( OutlineBrightness, settings.outlineBrightness );
			scanMaterial.SetFloat( OutlineStarDistance, settings.outlineStarDistance );

			scanMaterial.SetFloat( ScanLineWidth, settings.scanLineWidth );
			scanMaterial.SetFloat( ScanLineInterval, settings.scanLineInterval );
			scanMaterial.SetFloat( ScanLineBrightness, settings.scanLineBrightness );
			scanMaterial.SetFloat( ScanRange, settings.scanRange );

			scanMaterial.SetFloat( HeadScanLineDistance, settings.headScanLineDistance );
			scanMaterial.SetFloat( HeadScanLineWidth, settings.headScanLineWidth );

			scanMaterial.SetVector( ScanCenterWs, settings.scanCenterWS );
			_passName = "ScanEffect";
			this.settings = settings;
		}


		//在执行pass前执行，用来构造渲染目标和清除状态
		//同样用来创建临时RT
		//如果为空，则会渲染到激活的RT上
		public override void OnCameraSetup( CommandBuffer cmd, ref RenderingData renderingData ) {
			//获得相机颜色缓冲区，存到_cameraColor里
			_cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
			_cameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
			//获取屏幕纹理的描述器 
			m_Descriptor = new RenderTextureDescriptor( Screen.width, Screen.height, RenderTextureFormat.Default, 0 ){
				depthBufferBits = 0 //不需要深度缓冲区
			};
			//新建纹理_tempTex
			RenderingUtils.ReAllocateIfNeeded( ref _tempTex, m_Descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name:"_TempTex" );

			//这个用来在blit的时候指定目标RT（如果不指定，则默认为激活的RT）
			//blit如果不指定目标RT，则为这个RT
			ConfigureTarget( _tempTex );
		}

		//每帧会调用一次，应用Pass
		public override void Execute( ScriptableRenderContext context, ref RenderingData renderingData ) {
			//如果不是Game视图，就不执行
			if( renderingData.cameraData.camera.cameraType != CameraType.Game ) return;
			//如果没有材质，就不执行
			if( settings.scanMaterial == null ) return;

			//新建一个CommandBuffer
			//CommandBufferPool.Get()会从一个池子里获取CommandBuffer，如果池子里没有可用的CommandBuffer，就会新建一个
			CommandBuffer cmd = CommandBufferPool.Get( name:_passName );

			//创建一个frame debugger的作用域
			using( new ProfilingScope( cmd, new ProfilingSampler( cmd.name ) ) ) {
				Blitter.BlitCameraTexture( cmd, _cameraDepth, _cameraColor, settings.scanMaterial, 0 ); //blit到rt上

				if( showMark ) {
					cmd.SetRenderTarget( _cameraColor, _cameraDepth );
					var matProp = new MaterialPropertyBlock();
					_computeBuffer.SetData( _marks );
					matProp.SetBuffer( "markBuffer", _computeBuffer );
					_commandData[0].indexCountPerInstance = 6;
					_commandData[0].instanceCount = horizentalCount * verticalCount;
					_graphicsBuffer.SetData( _commandData );
					cmd.DrawMeshInstancedIndirect( mesh, 0, settings.markMaterial, 0, _graphicsBuffer, 0, matProp );
				}
			}

			//Blitter.BlitCameraTexture( cmd, _cameraColor, _tempTex );//blit效果到屏幕的color buffer上（后处理）
			//执行、清空、释放 CommandBuffer
			context.ExecuteCommandBuffer( cmd );
			cmd.Clear();
			CommandBufferPool.Release( cmd );
		}

		//清除任何分配的临时RT
		public override void OnCameraCleanup( CommandBuffer cmd ) {

		}

		~CustomRenderPass() {
			_graphicsBuffer.Dispose();
			_computeBuffer.Dispose();
			Debug.Log( "释放buffer" );
		}

	}

	/*************************************************************************/


	//当RendererFeature被创建、激活、改变参数时调用
	public override void Create() {
		if( settings.scanMaterial == null ) return;
		if( !Application.isPlaying ) return;

		_marks = new Marks[horizentalCount * verticalCount];
		//初始化CustomRenderPass
		_myPass = new CustomRenderPass( settings );
		_instance = this;
	}

	public override void SetupRenderPasses( ScriptableRenderer renderer, in RenderingData renderingData ) {
		if( settings.scanMaterial == null ) return;
		if( !Application.isPlaying ) return;

		if( renderingData.cameraData.cameraType == CameraType.Game ) {
			_myPass.renderPassEvent = settings.renderEvent;
			//声明要使用的颜色和深度缓冲区
			_myPass.ConfigureInput( ScriptableRenderPassInput.Color );
			_myPass.ConfigureInput( ScriptableRenderPassInput.Normal );
			_myPass.ConfigureInput( ScriptableRenderPassInput.Depth );
		}
	}

	//对每个相机调用一次，用来注入ScriptableRenderPass 
	public override void AddRenderPasses( ScriptableRenderer renderer,
		ref RenderingData renderingData ) {
		if( settings.scanMaterial == null ) return;
		if( !Application.isPlaying ) return;

		//注入CustomRenderPass，这样每帧就会调用CustomRenderPass的Execute()方法
		renderer.EnqueuePass( _myPass );
	}
}