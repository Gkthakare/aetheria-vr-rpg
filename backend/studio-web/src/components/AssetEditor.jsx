import React, { useState, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import './AssetEditor.css';

const AssetEditor = ({ assetId, assetName }) => {
  const containerRef = useRef(null);
  const sceneRef = useRef(null);
  const rendererRef = useRef(null);
  const modelRef = useRef(null);
  const controlsRef = useRef(null);
  const lightsRef = useRef({});
  
  // State
  const [model, setModel] = useState(null);
  const [transform, setTransform] = useState({
    position: { x: 0, y: 0, z: 0 },
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 1, y: 1, z: 1 }
  });
  
  const [material, setMaterial] = useState({
    color: '#ffffff',
    metalness: 0.5,
    roughness: 0.5
  });
  
  const [lighting, setLighting] = useState({
    ambientIntensity: 0.6,
    directionalIntensity: 1.2,
    pointIntensity: 0.8
  });

  const [animations, setAnimations] = useState([]);
  const [currentAnimation, setCurrentAnimation] = useState(null);
  const [isAnimationPlaying, setIsAnimationPlaying] = useState(false);
  const animationMixerRef = useRef(null);
  const clockRef = useRef(new THREE.Clock());
  
  const [editMode, setEditMode] = useState('transform'); // transform, material, lighting, animations, export
  const [saved, setSaved] = useState(true);

  // Initialize Three.js scene
  useEffect(() => {
    if (!containerRef.current) return;

    // Scene setup
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1a1a1a);
    sceneRef.current = scene;

    // Camera
    const camera = new THREE.PerspectiveCamera(
      75,
      containerRef.current.clientWidth / containerRef.current.clientHeight,
      0.1,
      1000
    );
    camera.position.z = 5;

    // Renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    renderer.shadowMap.enabled = true;
    containerRef.current.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 1.2);
    directionalLight.position.set(10, 15, 10);
    directionalLight.castShadow = true;
    scene.add(directionalLight);

    // Add a point light for better material visibility
    const pointLight = new THREE.PointLight(0xffffff, 0.8);
    pointLight.position.set(-10, 10, 5);
    scene.add(pointLight);

    lightsRef.current = {
      ambient: ambientLight,
      directional: directionalLight,
      point: pointLight
    };

    // Controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controlsRef.current = controls;

    // Load model
    const loader = new GLTFLoader();
    loader.load(
      `/api/assets/${assetId}/download`,
      (gltf) => {
        try {
          const modelGroup = gltf.scene;
          
          // Center model
          const box = new THREE.Box3().setFromObject(modelGroup);
          const center = box.getCenter(new THREE.Vector3());
          modelGroup.position.sub(center);
          
          scene.add(modelGroup);
          modelRef.current = modelGroup;
          setModel(modelGroup);

          // Setup animations
          if (gltf.animations && gltf.animations.length > 0) {
            const mixer = new THREE.AnimationMixer(modelGroup);
            animationMixerRef.current = mixer;
            setAnimations(gltf.animations.map((clip, idx) => ({
              id: idx,
              name: clip.name,
              duration: clip.duration,
              clip: clip
            })));
          }
        } catch (error) {
          console.error('Error processing loaded model:', error);
        }
      },
      (progress) => {
        console.log('Model loading progress:', (progress.loaded / progress.total) * 100 + '%');
      },
      (error) => {
        console.error('Error loading model:', error);
      }
    );

    // Animation loop
    const animate = () => {
      requestAnimationFrame(animate);
      controls.update();
      
      // Update animation mixer
      if (animationMixerRef.current && isAnimationPlaying) {
        const delta = clockRef.current.getDelta();
        animationMixerRef.current.update(delta);
      }
      
      renderer.render(scene, camera);
    };
    animate();

    // Handle resize
    const handleResize = () => {
      camera.aspect = containerRef.current.clientWidth / containerRef.current.clientHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(containerRef.current.clientWidth, containerRef.current.clientHeight);
    };
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      containerRef.current?.removeChild(renderer.domElement);
      renderer.dispose();
    };
  }, [assetId]);

  // Update transform
  const handleTransformChange = (axis, component, value) => {
    const newTransform = { ...transform };
    newTransform[axis][component] = parseFloat(value);
    setTransform(newTransform);
    setSaved(false);

    if (modelRef.current) {
      if (axis === 'position') {
        modelRef.current.position[component] = newTransform.position[component];
      } else if (axis === 'rotation') {
        modelRef.current.rotation[component] = newTransform.rotation[component];
      } else if (axis === 'scale') {
        modelRef.current.scale[component] = newTransform.scale[component];
      }
    }
  };

  // Update material
  const handleMaterialChange = (property, value) => {
    let numericValue = value;
    
    // Ensure metalness and roughness are numbers
    if (property === 'metalness' || property === 'roughness') {
      numericValue = parseFloat(value) || 0;
      numericValue = Math.max(0, Math.min(1, numericValue)); // Clamp 0-1
    }
    
    const newMaterial = { ...material, [property]: numericValue };
    setMaterial(newMaterial);
    setSaved(false);

    if (modelRef.current) {
      modelRef.current.traverse((child) => {
        if (child.isMesh && child.material) {
          try {
            if (property === 'color') {
              child.material.color.set(value);
            } else if (property === 'metalness') {
              // Ensure material supports metalness
              if ('metalness' in child.material) {
                child.material.metalness = numericValue;
              } else {
                console.warn('Material does not support metalness');
              }
            } else if (property === 'roughness') {
              // Ensure material supports roughness
              if ('roughness' in child.material) {
                child.material.roughness = numericValue;
              } else {
                console.warn('Material does not support roughness');
              }
            }
            child.material.needsUpdate = true;
          } catch (error) {
            console.error('Error updating material:', error);
          }
        }
      });
    }
  };

  // Save changes
  const handleSave = async () => {
    try {
      // Save transform
      await fetch(`/api/assets/${assetId}/transform`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ asset_id: assetId, ...transform })
      });

      // Save material
      await fetch(`/api/assets/${assetId}/material`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ asset_id: assetId, ...material })
      });

      setSaved(true);
      alert('Changes saved!');
    } catch (error) {
      console.error('Save failed:', error);
      alert('Failed to save changes');
    }
  };

  // Reset changes
  const handleReset = () => {
    setTransform({
      position: { x: 0, y: 0, z: 0 },
      rotation: { x: 0, y: 0, z: 0 },
      scale: { x: 1, y: 1, z: 1 }
    });
    setMaterial({
      color: '#ffffff',
      metalness: 0.5,
      roughness: 0.5
    });
    if (modelRef.current) {
      modelRef.current.position.set(0, 0, 0);
      modelRef.current.rotation.set(0, 0, 0);
      modelRef.current.scale.set(1, 1, 1);
    }
    setSaved(true);
  };

  // Update lighting
  const handleLightingChange = (light, intensity) => {
    const newLighting = { ...lighting, [light]: intensity };
    setLighting(newLighting);
    
    if (light === 'ambientIntensity' && lightsRef.current.ambient) {
      lightsRef.current.ambient.intensity = intensity;
    } else if (light === 'directionalIntensity' && lightsRef.current.directional) {
      lightsRef.current.directional.intensity = intensity;
    } else if (light === 'pointIntensity' && lightsRef.current.point) {
      lightsRef.current.point.intensity = intensity;
    }
  };

  // Play animation
  const playAnimation = (animationIndex) => {
    if (!animationMixerRef.current || !animations[animationIndex]) return;
    
    animationMixerRef.current.stopAllAction();
    const action = animationMixerRef.current.clipAction(animations[animationIndex].clip);
    action.play();
    
    setCurrentAnimation(animationIndex);
    setIsAnimationPlaying(true);
  };

  // Stop animation
  const stopAnimation = () => {
    if (animationMixerRef.current) {
      animationMixerRef.current.stopAllAction();
    }
    setIsAnimationPlaying(false);
    setCurrentAnimation(null);
  };

  // Export model
  const handleExport = async () => {
    try {
      const link = document.createElement('a');
      link.href = `/api/assets/${assetId}/download`;
      link.download = `${assetName}_edited.glb`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch (error) {
      console.error('Export failed:', error);
      alert('Failed to export model');
    }
  };

  return (
    <div className="asset-editor">
      <div className="editor-header">
        <h2>{assetName}</h2>
        <div className="status">
          {saved ? <span className="saved">✓ Saved</span> : <span className="unsaved">● Unsaved</span>}
        </div>
      </div>

      <div className="editor-container">
        <div className="canvas-container" ref={containerRef} />
        
        <div className="controls-panel">
          <div className="mode-tabs">
            <button 
              className={`tab ${editMode === 'transform' ? 'active' : ''}`}
              onClick={() => setEditMode('transform')}
            >
              Transform
            </button>
            <button 
              className={`tab ${editMode === 'material' ? 'active' : ''}`}
              onClick={() => setEditMode('material')}
            >
              Material
            </button>
            <button 
              className={`tab ${editMode === 'lighting' ? 'active' : ''}`}
              onClick={() => setEditMode('lighting')}
            >
              💡 Lighting
            </button>
            {animations.length > 0 && (
              <button 
                className={`tab ${editMode === 'animations' ? 'active' : ''}`}
                onClick={() => setEditMode('animations')}
              >
                🎬 Animations
              </button>
            )}
            <button 
              className={`tab ${editMode === 'export' ? 'active' : ''}`}
              onClick={() => setEditMode('export')}
            >
              📥 Export
            </button>
          </div>

          {editMode === 'transform' && (
            <div className="control-group">
              <h3>Position</h3>
              {['x', 'y', 'z'].map(axis => (
                <div key={`pos-${axis}`} className="input-group">
                  <label>{axis.toUpperCase()}</label>
                  <input
                    type="number"
                    step="0.1"
                    value={transform.position[axis]}
                    onChange={(e) => handleTransformChange('position', axis, e.target.value)}
                  />
                </div>
              ))}

              <h3>Rotation</h3>
              {['x', 'y', 'z'].map(axis => (
                <div key={`rot-${axis}`} className="input-group">
                  <label>{axis.toUpperCase()}</label>
                  <input
                    type="number"
                    step="0.1"
                    value={transform.rotation[axis]}
                    onChange={(e) => handleTransformChange('rotation', axis, e.target.value)}
                  />
                </div>
              ))}

              <h3>Scale</h3>
              {['x', 'y', 'z'].map(axis => (
                <div key={`scale-${axis}`} className="input-group">
                  <label>{axis.toUpperCase()}</label>
                  <input
                    type="number"
                    step="0.1"
                    value={transform.scale[axis]}
                    onChange={(e) => handleTransformChange('scale', axis, e.target.value)}
                  />
                </div>
              ))}
            </div>
          )}

          {editMode === 'material' && (
            <div className="control-group">
              <h3>Color</h3>
              <div className="input-group">
                <label>Hex Color</label>
                <input
                  type="color"
                  value={material.color}
                  onChange={(e) => handleMaterialChange('color', e.target.value)}
                />
              </div>

              <h3>Metalness</h3>
              <div className="input-group">
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.01"
                  value={material.metalness}
                  onChange={(e) => handleMaterialChange('metalness', e.target.value)}
                />
                <span>{parseFloat(material.metalness).toFixed(2)}</span>
              </div>

              <h3>Roughness</h3>
              <div className="input-group">
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.01"
                  value={material.roughness}
                  onChange={(e) => handleMaterialChange('roughness', e.target.value)}
                />
                <span>{parseFloat(material.roughness).toFixed(2)}</span>
              </div>
            </div>
          )}

          {editMode === 'lighting' && (
            <div className="control-group">
              <h3>Ambient Light</h3>
              <div className="input-group">
                <input
                  type="range"
                  min="0"
                  max="2"
                  step="0.01"
                  value={lighting.ambientIntensity}
                  onChange={(e) => handleLightingChange('ambientIntensity', parseFloat(e.target.value))}
                />
                <span>{parseFloat(lighting.ambientIntensity).toFixed(2)}</span>
              </div>

              <h3>Directional Light</h3>
              <div className="input-group">
                <input
                  type="range"
                  min="0"
                  max="2"
                  step="0.01"
                  value={lighting.directionalIntensity}
                  onChange={(e) => handleLightingChange('directionalIntensity', parseFloat(e.target.value))}
                />
                <span>{parseFloat(lighting.directionalIntensity).toFixed(2)}</span>
              </div>

              <h3>Point Light</h3>
              <div className="input-group">
                <input
                  type="range"
                  min="0"
                  max="2"
                  step="0.01"
                  value={lighting.pointIntensity}
                  onChange={(e) => handleLightingChange('pointIntensity', parseFloat(e.target.value))}
                />
                <span>{parseFloat(lighting.pointIntensity).toFixed(2)}</span>
              </div>
            </div>
          )}

          {editMode === 'animations' && (
            <div className="control-group">
              <h3>Available Animations</h3>
              {animations.length === 0 ? (
                <p className="no-animations">No animations found</p>
              ) : (
                <div className="animations-list">
                  {animations.map((anim, idx) => (
                    <div key={anim.id} className="animation-item">
                      <span className="anim-name">{anim.name}</span>
                      <span className="anim-duration">{anim.duration.toFixed(2)}s</span>
                      <button
                        className="anim-btn"
                        onClick={() => playAnimation(idx)}
                        disabled={currentAnimation === idx && isAnimationPlaying}
                      >
                        {currentAnimation === idx && isAnimationPlaying ? '▶️ Playing' : '▶️ Play'}
                      </button>
                    </div>
                  ))}
                </div>
              )}
              {isAnimationPlaying && (
                <button className="btn-stop" onClick={stopAnimation}>⏹️ Stop</button>
              )}
            </div>
          )}

          {editMode === 'export' && (
            <div className="control-group">
              <h3>Export Options</h3>
              <p className="export-info">Export your edited 3D model as a GLB file</p>
              <button className="btn-export" onClick={handleExport}>
                📥 Download GLB File
              </button>
            </div>
          )}

          <div className="action-buttons">
            <button className="btn-save" onClick={handleSave}>Save Changes</button>
            <button className="btn-reset" onClick={handleReset}>Reset</button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AssetEditor;
