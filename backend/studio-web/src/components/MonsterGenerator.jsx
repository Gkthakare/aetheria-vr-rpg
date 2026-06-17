import React, { useEffect, useMemo, useRef, useState } from 'react';
import './MonsterGenerator.css';

function MonsterGenerator({ assets, onAssetSaved }) {
  const [prompt, setPrompt] = useState('');
  const [candidateCount, setCandidateCount] = useState(3);
  const [messages, setMessages] = useState([]);
  const [job, setJob] = useState(null);
  const [selectedCandidateId, setSelectedCandidateId] = useState(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const messagesEndRef = useRef(null);
  const pollTimeoutRef = useRef(null);
  const synth = typeof window !== 'undefined' ? window.speechSynthesis : null;

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    return () => {
      if (pollTimeoutRef.current) {
        window.clearTimeout(pollTimeoutRef.current);
      }
      synth?.cancel?.();
    };
  }, [synth]);

  const selectedCandidate = useMemo(() => {
    return job?.candidates?.find(candidate => candidate.id === selectedCandidateId) || null;
  }, [job, selectedCandidateId]);

  const addMessage = (text, type = 'info', voice = false) => {
    setMessages(previous => [...previous, { text, type, id: Date.now() + Math.random() }]);
    if (voice && synth) {
      synth.cancel();
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.rate = 0.95;
      utterance.pitch = Math.random() * 0.4 + 0.9;
      utterance.volume = 1;
      synth.speak(utterance);
    }
  };

  const stopPolling = () => {
    if (pollTimeoutRef.current) {
      window.clearTimeout(pollTimeoutRef.current);
      pollTimeoutRef.current = null;
    }
  };

  const fetchJobStatus = async (jobId, attempt = 0) => {
    try {
      const response = await fetch(`/api/generations/${jobId}`);
      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.detail || 'Failed to load generation job');
      }

      setJob(data);

      if (data.candidates?.length && !selectedCandidateId) {
        setSelectedCandidateId(data.candidates[0].id);
      }

      if (data.status === 'ready') {
        setIsGenerating(false);
        addMessage(`Generated ${data.candidates.length} monster candidates. Pick one to download or add to the gallery.`, 'success', true);
        stopPolling();
        return;
      }

      if (data.status === 'failed') {
        setIsGenerating(false);
        addMessage(data.error || 'Generation failed.', 'error', true);
        stopPolling();
        return;
      }

      if (attempt < 20) {
        pollTimeoutRef.current = window.setTimeout(() => {
          fetchJobStatus(jobId, attempt + 1);
        }, 1500);
      } else {
        setIsGenerating(false);
        addMessage('Generation is taking longer than expected. You can retry with the same prompt.', 'error');
      }
    } catch (error) {
      setIsGenerating(false);
      addMessage(`Unable to refresh generation status: ${error.message}`, 'error');
      stopPolling();
    }
  };

  const handleGenerate = async () => {
    const trimmedPrompt = prompt.trim();
    if (!trimmedPrompt || isGenerating) {
      return;
    }

    stopPolling();
    setIsGenerating(true);
    setJob(null);
    setSelectedCandidateId(null);
    setMessages([]);

    addMessage('Prompt received. Starting a new monster generation job...', 'info', true);
    if (assets.length > 0) {
      addMessage(`Using ${assets.length} gallery asset${assets.length === 1 ? '' : 's'} as optional inspiration for the offline MVP provider.`, 'info');
    } else {
      addMessage('No gallery assets found, so the offline MVP provider will create procedural glTF monsters from the prompt alone.', 'info');
    }

    try {
      const response = await fetch('/api/generations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          prompt: trimmedPrompt,
          candidateCount: Number(candidateCount),
          options: {}
        })
      });

      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.detail || 'Failed to start generation');
      }

      setJob(data);

      if (data.candidates?.length) {
        setSelectedCandidateId(data.candidates[0].id);
      }

      addMessage(`Generation job #${data.id} created for "${trimmedPrompt}".`, 'success');

      if (data.status === 'ready') {
        setIsGenerating(false);
        addMessage(`All ${data.candidates.length} candidates are ready. Select your favorite monster.`, 'success', true);
      } else {
        addMessage('Generation is still running. Polling for results...', 'info');
        fetchJobStatus(data.id);
      }
    } catch (error) {
      setIsGenerating(false);
      addMessage(`Generation failed: ${error.message}`, 'error', true);
    }
  };

  const handleCandidateSelect = (candidateId) => {
    setSelectedCandidateId(candidateId);
    const candidate = job?.candidates?.find(item => item.id === candidateId);
    if (candidate) {
      addMessage(`Selected ${candidate.name}.`, 'info');
    }
  };

  const handleDownload = () => {
    if (!job || !selectedCandidate) {
      return;
    }

    addMessage(`Downloading ${selectedCandidate.name}...`, 'info');
    const link = document.createElement('a');
    link.href = `/api/generations/${job.id}/candidates/${selectedCandidate.id}/download`;
    link.download = `${selectedCandidate.name}.glb`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    addMessage('Download started.', 'success');
  };

  const handleAddToGallery = async () => {
    if (!job || !selectedCandidate || isSaving) {
      return;
    }

    setIsSaving(true);
    addMessage(`Saving ${selectedCandidate.name} to the gallery...`, 'info', true);

    try {
      const response = await fetch(`/api/generations/${job.id}/select`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          candidateId: selectedCandidate.id,
          name: selectedCandidate.name,
          description: selectedCandidate.description
        })
      });

      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.detail || 'Failed to add monster to gallery');
      }

      addMessage(`${selectedCandidate.name} was added to the gallery.`, 'success', true);
      if (onAssetSaved) {
        onAssetSaved(data.asset);
      }
    } catch (error) {
      addMessage(`Failed to save candidate: ${error.message}`, 'error');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="monster-generator">
      <div className="generator-container">
        <div className="generator-header">
          <h2>Prompt To 3D Monster</h2>
          <p>Describe the creature you want, generate multiple candidates, then choose one to download or add to the gallery.</p>
        </div>

        <div className="generator-form-panel">
          <div className="form-grid">
            <label className="field-group prompt-field">
              <span>Monster prompt</span>
              <textarea
                value={prompt}
                onChange={(event) => setPrompt(event.target.value)}
                placeholder="Example: lava armored monster with four arms, crystal horns, glowing blue eyes"
                rows={4}
              />
            </label>

            <label className="field-group count-field">
              <span>Candidate count</span>
              <select value={candidateCount} onChange={(event) => setCandidateCount(event.target.value)}>
                <option value={2}>2 candidates</option>
                <option value={3}>3 candidates</option>
                <option value={4}>4 candidates</option>
                <option value={5}>5 candidates</option>
              </select>
            </label>
          </div>

          <div className="generator-actions">
            <button
              className={`generate-btn ${isGenerating ? 'generating' : ''}`}
              onClick={handleGenerate}
              disabled={isGenerating || !prompt.trim()}
            >
              {isGenerating ? (
                <>
                  <span className="spinner"></span>
                  Generating candidates...
                </>
              ) : (
                'Generate Monster Candidates'
              )}
            </button>
            <p className="helper-text">
              Current MVP uses a local offline provider that generates simple procedural glTF monsters directly from your prompt, with gallery assets used only as optional inspiration.
            </p>
          </div>
        </div>

        <div className="generator-layout">
          <section className="generator-panel">
            <div className="panel-header">
              <h3>Generation Log</h3>
              {job && <span className={`status-pill status-${job.status}`}>{job.status}</span>}
            </div>
            <div className="messages-list">
              {messages.length === 0 && (
                <div className="message message-info">
                  Enter a prompt and start a generation job to see system updates here.
                </div>
              )}
              {messages.map(message => (
                <div key={message.id} className={`message message-${message.type}`}>
                  {message.text}
                </div>
              ))}
              <div ref={messagesEndRef} />
            </div>
          </section>

          <section className="generator-panel">
            <div className="panel-header">
              <h3>Candidate Results</h3>
              {job?.candidates?.length ? <span>{job.candidates.length} ready</span> : null}
            </div>

            {!job?.candidates?.length ? (
              <div className="empty-candidates">
                <p>No generated candidates yet.</p>
                <p>Start with a detailed prompt, then choose the candidate you want to keep.</p>
              </div>
            ) : (
              <>
                <div className="candidate-grid">
                  {job.candidates.map(candidate => {
                    const traits = candidate.metadata?.traits || {};
                    const isSelected = candidate.id === selectedCandidateId;

                    return (
                      <button
                        key={candidate.id}
                        type="button"
                        className={`candidate-card ${isSelected ? 'selected' : ''}`}
                        onClick={() => handleCandidateSelect(candidate.id)}
                      >
                        <div className="candidate-card-header">
                          <div>
                            <h4>{candidate.name}</h4>
                            <p>{candidate.preview_label || 'Prompt candidate'}</p>
                          </div>
                          <span className="file-chip">.{candidate.file_format}</span>
                        </div>
                        <p className="candidate-description">{candidate.description}</p>
                        <div className="candidate-stats">
                          <span>Size: {traits.size || 'n/a'}</span>
                          <span>Element: {traits.element || 'n/a'}</span>
                          <span>Rarity: {traits.rarity || 'n/a'}</span>
                          <span>Heads: {traits.headCount || 'n/a'}</span>
                        </div>
                      </button>
                    );
                  })}
                </div>

                {selectedCandidate && (
                  <div className="selected-candidate-panel">
                    <div className="panel-header">
                      <h3>Selected Monster</h3>
                      <span>{selectedCandidate.name}</span>
                    </div>
                    <p>{selectedCandidate.description}</p>
                    <div className="selected-actions">
                      <button className="action-btn" onClick={handleDownload}>
                        Download
                      </button>
                      <button className="action-btn action-btn-primary" onClick={handleAddToGallery} disabled={isSaving}>
                        {isSaving ? 'Adding To Gallery...' : 'Add To Gallery'}
                      </button>
                    </div>
                  </div>
                )}
              </>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}

export default MonsterGenerator;
