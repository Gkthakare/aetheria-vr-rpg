# 3D Monster Asset Dashboard - Frontend

React + Three.js frontend for the interactive 3D asset management system.

## Features

- **Asset Gallery**: Browse and manage all 3D assets
- **3D Viewer**: Real-time 3D asset preview with Three.js
- **Interactive Editing**:
  - Transform: Position, rotation, scale adjustments
  - Material: Color, metalness, roughness editing
  - Real-time preview of changes
- **Asset Management**: Upload, download, delete assets
- **Edit History**: Track all modifications to assets

## Setup

### Prerequisites
- Node.js 16+ and npm

### Installation

1. Install dependencies:
```bash
npm install
```

2. Start development server:
```bash
npm run dev
```

The app will be available at `http://localhost:5173`

## Building for Production

```bash
npm run build
```

This generates optimized production build in `dist/` folder.

## Project Structure

```
src/
├── App.jsx              # Main app component
├── App.css              # Global styles
├── components/
│   ├── AssetGallery.jsx    # Asset gallery grid
│   ├── AssetGallery.css    # Gallery styles
│   ├── AssetEditor.jsx     # 3D editor component
│   └── AssetEditor.css     # Editor styles
├── index.jsx            # React entry point
└── index.css            # Global styles
```

## Component Overview

### AssetGallery
- Lists all 3D assets
- Upload new assets
- Delete/download assets
- Navigate to editor

### AssetEditor
- Three.js 3D viewer
- Transform controls (position, rotation, scale)
- Material editor (color, metalness, roughness)
- Save/reset changes
- Real-time preview

## API Integration

Connects to FastAPI backend at:
- Development: `http://localhost:8000`
- Production: Configure via environment variables

### Supported File Formats
- GLB (glTF Binary)
- GLTF (glTF ASCII)
- OBJ
- FBX

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## Technologies Used

- **React 18**: UI framework
- **Vite**: Build tool and dev server
- **Three.js**: 3D graphics library
- **CSS3**: Styling with gradients and animations
