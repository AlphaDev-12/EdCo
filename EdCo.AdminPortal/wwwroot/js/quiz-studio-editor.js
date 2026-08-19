/**
 * EdCo Admin Portal - Quiz Studio Editor Module
 * Manages question creation, editing, AI rubric generation, image scanning, and Cropper.js integrations.
 */

(function () {
    // Read model configuration from script tag
    let config = { quizId: 0, subjectName: '', questions: [] };
    const configEl = document.getElementById('quiz-editor-data');
    if (configEl) {
        try {
            config = JSON.parse(configEl.textContent || '{}');
        } catch (e) {
            console.error('[QuizStudioEditor] Error parsing quiz editor configuration:', e);
        }
    }

    const quizId = config.quizId || 0;
    const currentSubjectName = config.subjectName || '';
    const questionsData = config.questions || [];

    let cropper = null;
    let currentScanTarget = 'question';
    let scannedDiagramBase64 = null;
    let scannedAnswerDiagramBase64 = null;
    let editScannedDiagramBase64 = null;
    let editScannedAnswerDiagramBase64 = null;

    // Helper functions
    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }

    function toggleQuestionType() {
        const type = document.getElementById('newQuestionType').value;
        if (type == "0") {
            document.getElementById('mcOptionsSection').style.display = 'block';
            document.getElementById('aiGradingSection').style.display = 'none';
        } else {
            document.getElementById('mcOptionsSection').style.display = 'none';
            document.getElementById('aiGradingSection').style.display = 'block';
        }
    }

    function addRubricRow() {
        const container = document.getElementById('rubricTableBody');
        const row = document.createElement('div');
        row.className = 'row align-items-center mb-3 mb-md-2 pb-3 pb-md-0 border-bottom border-secondary border-opacity-25 rubric-row';
        row.innerHTML = `
            <div class="col-12 col-md-3 mb-2 mb-md-0">
                <label class="form-label d-md-none text-muted small mb-1">Criterion</label>
                <input type="text" class="form-control form-control-sm rubric-criterion" placeholder="e.g. Formatting" />
            </div>
            <div class="col-12 col-md-2 mb-2 mb-md-0">
                <label class="form-label d-md-none text-muted small mb-1">Max Pts</label>
                <input type="number" class="form-control form-control-sm rubric-points" value="1" min="0" />
            </div>
            <div class="col-12 col-md-6 mb-2 mb-md-0">
                <label class="form-label d-md-none text-muted small mb-1">Description</label>
                <input type="text" class="form-control form-control-sm rubric-desc" placeholder="Explanation of requirements..." />
            </div>
            <div class="col-12 col-md-1 text-end text-md-center">
                <button type="button" class="btn btn-sm btn-outline-danger w-100 w-md-auto" onclick="window.EdCoQuizEditor.removeRubricRow(this)"><i class="fa-solid fa-times"></i> <span class="d-md-none ms-1">Remove</span></button>
            </div>
        `;
        container.appendChild(row);
    }

    function removeRubricRow(btn) {
        const row = btn.closest('.rubric-row');
        if (row) row.remove();
    }

    function getRubricJson() {
        const rows = document.querySelectorAll('.rubric-row');
        const rubricArray = [];
        rows.forEach(row => {
            const criterion = row.querySelector('.rubric-criterion').value.trim();
            const points = parseInt(row.querySelector('.rubric-points').value, 10) || 0;
            const desc = row.querySelector('.rubric-desc').value.trim();

            if (criterion) {
                rubricArray.push({
                    Criterion: criterion,
                    MaxPoints: points,
                    Description: desc
                });
            }
        });
        return JSON.stringify(rubricArray);
    }

    function addQuestion() {
        const typeStr = document.getElementById('newQuestionType').value;
        const type = parseInt(typeStr, 10);

        let rubricJsonStr = "";
        if (type !== 0) {
            rubricJsonStr = getRubricJson();
        }

        submitQuestionData(type, rubricJsonStr, scannedDiagramBase64, scannedAnswerDiagramBase64);
    }

    function submitQuestionData(type, rubricJsonStr, imageUrl, correctAnswerImageUrl) {
        const data = {
            quizId: quizId,
            questionText: document.getElementById('newQuestionText').value,
            questionType: type,
            points: parseInt(document.getElementById('newPoints').value, 10) || 1,
            optionA: document.getElementById('newOptionA').value,
            optionB: document.getElementById('newOptionB').value,
            optionC: document.getElementById('newOptionC').value,
            optionD: document.getElementById('newOptionD').value,
            correctOption: document.getElementById('newCorrectOption').value,
            correctAnswer: document.getElementById('newCorrectAnswer').value,
            correctAnswerImageUrl: correctAnswerImageUrl,
            rubricJson: rubricJsonStr,
            imageUrl: imageUrl
        };

        if (!data.questionText) {
            alert('Please fill in the question text.');
            return;
        }

        if (type === 0 && (!data.optionA || !data.optionB)) {
            alert('Please fill in at least options A and B for Multiple Choice.');
            return;
        }

        if (type !== 0 && (!data.rubricJson || data.rubricJson === "[]")) {
            alert('Please define at least one grading criterion in the rubric for the AI.');
            return;
        }

        fetch('/QuizStudio/AddQuestion', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
        .then(r => r.json())
        .then(result => {
            if (result.success) {
                location.reload();
            }
        });
    }

    function deleteQuestion(id) {
        if (!confirm('Delete this question?')) return;

        fetch('/QuizStudio/DeleteQuestion?id=' + id, { method: 'POST' })
        .then(r => r.json())
        .then(result => {
            if (result.success) {
                const el = document.getElementById('question-' + id);
                if (el) el.remove();
            }
        });
    }

    function updateExamStatus() {
        const pos = document.getElementById('examDisplayPos').value;
        fetch('/QuizStudio/UpdateExamStatus', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                quizId: quizId,
                displayPosition: parseInt(pos, 10) || 0
            })
        })
        .then(r => r.json())
        .then(result => {
            if (result.success) {
                alert('Exam position updated!');
            }
        });
    }

    function updateQuizTitle() {
        const title = document.getElementById('quizTitle').value;
        fetch('/QuizStudio/UpdateTitle', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                quizId: quizId,
                title: title
            })
        })
        .then(r => r.json())
        .then(result => {
            if (result.success) {
                alert('Title updated!');
                location.reload();
            }
        });
    }

    function triggerScan(target) {
        currentScanTarget = target;
        document.getElementById('cameraInput').click();
    }

    function removeDiagramImage() {
        scannedDiagramBase64 = null;
        document.getElementById('diagramPreview').src = '';
        document.getElementById('diagramPreviewContainer').style.display = 'none';
        document.getElementById('btnRemoveDiagram').style.display = 'none';
    }

    function removeAnswerDiagramImage() {
        scannedAnswerDiagramBase64 = null;
        document.getElementById('answerDiagramPreview').src = '';
        document.getElementById('answerDiagramPreviewContainer').style.display = 'none';
        document.getElementById('btnRemoveAnswerDiagram').style.display = 'none';
    }

    function processCameraImage(input) {
        if (!input.files || input.files.length === 0) return;

        const file = input.files[0];
        const reader = new FileReader();

        reader.onload = function(e) {
            const tempImg = new Image();
            tempImg.onload = function() {
                const maxDim = 1600;
                let w = tempImg.width;
                let h = tempImg.height;

                let dataUrl = e.target.result;

                if (w > maxDim || h > maxDim) {
                    if (w > h) {
                        h = Math.round((h * maxDim) / w);
                        w = maxDim;
                    } else {
                        w = Math.round((w * maxDim) / h);
                        h = maxDim;
                    }
                    const c = document.createElement('canvas');
                    c.width = w;
                    c.height = h;
                    c.getContext('2d').drawImage(tempImg, 0, 0, w, h);
                    dataUrl = c.toDataURL('image/jpeg', 0.85);
                }

                const cropImage = document.getElementById('cropImage');
                cropImage.src = dataUrl;

                const cropModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('cropModal'));
                cropModal.show();
            };
            tempImg.onerror = function() {
                alert('Could not load the captured image.');
            };
            tempImg.src = e.target.result;
        };

        reader.readAsDataURL(file);
        input.value = '';
    }

    function editQuestion(id) {
        const q = questionsData.find(x => x.id === id);
        if (!q) return;

        document.getElementById('editQuestionId').value = q.id;
        document.getElementById('editQuestionText').value = q.questionText || '';
        document.getElementById('editQuestionType').value = q.questionType;
        document.getElementById('editPoints').value = q.points || 1;

        document.getElementById('editOptionA').value = q.optionA || '';
        document.getElementById('editOptionB').value = q.optionB || '';
        document.getElementById('editOptionC').value = q.optionC || '';
        document.getElementById('editOptionD').value = q.optionD || '';
        document.getElementById('editCorrectOption').value = q.correctOption || 'A';

        document.getElementById('editCorrectAnswer').value = q.correctAnswer || '';

        editScannedDiagramBase64 = q.imageUrl || null;
        const diagramPrev = document.getElementById('editDiagramPreview');
        const diagramContainer = document.getElementById('editDiagramPreviewContainer');
        const btnRemove = document.getElementById('btnRemoveEditDiagram');

        if (q.imageUrl && q.imageUrl.trim()) {
            diagramPrev.src = q.imageUrl;
            diagramContainer.style.display = 'block';
            btnRemove.style.display = 'inline-block';
        } else {
            diagramPrev.src = '';
            diagramContainer.style.display = 'none';
            btnRemove.style.display = 'none';
        }

        editScannedAnswerDiagramBase64 = q.correctAnswerImageUrl || null;
        const ansDiagramPrev = document.getElementById('editAnswerDiagramPreview');
        const ansDiagramContainer = document.getElementById('editAnswerDiagramPreviewContainer');
        const btnRemoveAns = document.getElementById('btnRemoveEditAnswerDiagram');

        if (q.correctAnswerImageUrl && q.correctAnswerImageUrl.trim()) {
            ansDiagramPrev.src = q.correctAnswerImageUrl;
            ansDiagramContainer.style.display = 'block';
            btnRemoveAns.style.display = 'inline-block';
        } else {
            ansDiagramPrev.src = '';
            ansDiagramContainer.style.display = 'none';
            btnRemoveAns.style.display = 'none';
        }

        const container = document.getElementById('editRubricTableBody');
        container.innerHTML = '';
        if (q.rubricJson && q.rubricJson.trim()) {
            try {
                const criteria = JSON.parse(q.rubricJson);
                if (Array.isArray(criteria)) {
                    criteria.forEach(c => {
                        addEditRubricRow(c.Criterion || c.criterion || '', c.MaxPoints ?? c.maxPoints ?? 1, c.Description || c.description || '');
                    });
                }
            } catch(e) {
                console.error("Failed to parse RubricJson for edit:", e);
            }
        }
        if (container.children.length === 0) {
            addEditRubricRow("Core Concept", 1, "Clear explanation of the required concept.");
        }

        toggleEditQuestionType();
        updateEditQuestionPreview();
        updateEditAnswerPreview();

        const editModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('editQuestionModal'));
        editModal.show();
    }

    function toggleEditQuestionType() {
        const type = document.getElementById('editQuestionType').value;
        if (type == "0") {
            document.getElementById('editMcOptionsSection').style.display = 'block';
            document.getElementById('editAiGradingSection').style.display = 'none';
        } else {
            document.getElementById('editMcOptionsSection').style.display = 'none';
            document.getElementById('editAiGradingSection').style.display = 'block';
        }
    }

    function addEditRubricRow(criterion = '', points = 1, desc = '') {
        const container = document.getElementById('editRubricTableBody');
        const row = document.createElement('div');
        row.className = 'row align-items-center mb-3 mb-md-2 pb-3 pb-md-0 border-bottom border-secondary border-opacity-25 edit-rubric-row';
        row.innerHTML = `
            <div class="col-12 col-md-3 mb-2 mb-md-0">
                <label class="form-label d-md-none text-muted small mb-1">Criterion</label>
                <input type="text" class="form-control form-control-sm edit-rubric-criterion" placeholder="e.g. Core Concept" value="${escapeHtml(criterion)}" />
            </div>
            <div class="col-12 col-md-2 mb-2 mb-md-0">
                <label class="form-label d-md-none text-muted small mb-1">Max Pts</label>
                <input type="number" class="form-control form-control-sm edit-rubric-points" value="${points}" min="0" />
            </div>
            <div class="col-12 col-md-6 mb-2 mb-md-0">
                <label class="form-label d-md-none text-muted small mb-1">Description</label>
                <input type="text" class="form-control form-control-sm edit-rubric-desc" placeholder="Explanation of requirements..." value="${escapeHtml(desc)}" />
            </div>
            <div class="col-12 col-md-1 text-end text-md-center">
                <button type="button" class="btn btn-sm btn-outline-danger w-100 w-md-auto" onclick="window.EdCoQuizEditor.removeEditRubricRow(this)"><i class="fa-solid fa-times"></i> <span class="d-md-none ms-1">Remove</span></button>
            </div>
        `;
        container.appendChild(row);
    }

    function removeEditRubricRow(btn) {
        const row = btn.closest('.edit-rubric-row');
        if (row) row.remove();
    }

    function getEditRubricJson() {
        const rows = document.querySelectorAll('.edit-rubric-row');
        const rubricArray = [];
        rows.forEach(row => {
            const criterion = row.querySelector('.edit-rubric-criterion').value.trim();
            const points = parseInt(row.querySelector('.edit-rubric-points').value, 10) || 0;
            const desc = row.querySelector('.edit-rubric-desc').value.trim();

            if (criterion) {
                rubricArray.push({
                    Criterion: criterion,
                    MaxPoints: points,
                    Description: desc
                });
            }
        });
        return JSON.stringify(rubricArray);
    }

    function removeEditDiagramImage() {
        editScannedDiagramBase64 = "";
        document.getElementById('editDiagramPreview').src = '';
        document.getElementById('editDiagramPreviewContainer').style.display = 'none';
        document.getElementById('btnRemoveEditDiagram').style.display = 'none';
    }

    function removeEditAnswerDiagramImage() {
        editScannedAnswerDiagramBase64 = "";
        document.getElementById('editAnswerDiagramPreview').src = '';
        document.getElementById('editAnswerDiagramPreviewContainer').style.display = 'none';
        document.getElementById('btnRemoveEditAnswerDiagram').style.display = 'none';
    }

    function updateEditQuestionPreview() {
        const text = document.getElementById('editQuestionText').value;
        const previewEl = document.getElementById('editQuestionTextPreview');
        if (!previewEl) return;

        if (!text || !text.trim()) {
            previewEl.innerHTML = '<span class="text-muted fst-italic">Student preview will render here...</span>';
            return;
        }

        previewEl.textContent = text;
        if (window.MathJax && window.MathJax.typesetPromise) {
            window.MathJax.typesetClear([previewEl]);
            window.MathJax.typesetPromise([previewEl]).catch(err => console.log('MathJax error:', err));
        }
    }

    function updateEditAnswerPreview() {
        const text = document.getElementById('editCorrectAnswer').value;
        const previewEl = document.getElementById('editCorrectAnswerPreview');
        if (!previewEl) return;

        if (!text || !text.trim()) {
            previewEl.innerHTML = '<span class="text-muted fst-italic">Answer preview will render here...</span>';
            return;
        }

        previewEl.textContent = text;
        if (window.MathJax && window.MathJax.typesetPromise) {
            window.MathJax.typesetClear([previewEl]);
            window.MathJax.typesetPromise([previewEl]).catch(err => console.log('MathJax error:', err));
        }
    }

    function updateQuestionPreview() {
        const text = document.getElementById('newQuestionText').value;
        const previewEl = document.getElementById('questionTextPreview');
        if (!previewEl) return;

        if (!text || !text.trim()) {
            previewEl.innerHTML = '<span class="text-muted fst-italic">Student preview will render here...</span>';
            return;
        }

        previewEl.textContent = text;
        if (window.MathJax && window.MathJax.typesetPromise) {
            window.MathJax.typesetClear([previewEl]);
            window.MathJax.typesetPromise([previewEl]).catch(err => console.log('MathJax error:', err));
        }
    }

    function updateAnswerPreview() {
        const text = document.getElementById('newCorrectAnswer').value;
        const previewEl = document.getElementById('correctAnswerPreview');
        if (!previewEl) return;

        if (!text || !text.trim()) {
            previewEl.innerHTML = '<span class="text-muted fst-italic">Answer preview will render here...</span>';
            return;
        }

        previewEl.textContent = text;
        if (window.MathJax && window.MathJax.typesetPromise) {
            window.MathJax.typesetClear([previewEl]);
            window.MathJax.typesetPromise([previewEl]).catch(err => console.log('MathJax error:', err));
        }
    }

    function generateEditRubricWithAI() {
        const questionText = document.getElementById('editQuestionText').value.trim();
        const refAnswer = document.getElementById('editCorrectAnswer').value.trim();

        if (!questionText) {
            alert('Please fill in the Question Text first before generating a rubric.');
            return;
        }
        if (!refAnswer) {
            alert('Please fill in the Reference Correct Answer before generating a rubric.');
            return;
        }

        const pointsInput = document.getElementById('editPoints');
        const userPoints = pointsInput ? (parseInt(pointsInput.value, 10) || 0) : 0;

        const overlay = document.getElementById('editRubricOverlay');
        const btn = document.getElementById('btnGenerateEditRubric');

        if (overlay) overlay.style.setProperty('display', 'flex', 'important');
        if (btn) btn.disabled = true;

        fetch('/QuizStudio/GenerateRubric', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                questionText: questionText,
                referenceAnswer: refAnswer,
                referenceAnswerImageUrl: editScannedAnswerDiagramBase64,
                questionImageUrl: editScannedDiagramBase64,
                points: userPoints
            })
        })
        .then(async r => {
            if (r.status === 401 || (r.redirected && r.url.includes('/Account/Login'))) {
                window.location.href = '/Account/Login';
                return { success: false, message: 'Session expired. Redirecting to login...' };
            }
            const text = await r.text();
            try {
                return JSON.parse(text);
            } catch(e) {
                return { success: false, message: 'Server error: ' + text.substring(0, 200) };
            }
        })
        .then(result => {
            if (overlay) overlay.style.setProperty('display', 'none', 'important');
            if (btn) btn.disabled = false;

            if (result.success && result.criteria && result.criteria.length > 0) {
                const container = document.getElementById('editRubricTableBody');
                container.innerHTML = '';

                result.criteria.forEach(item => {
                    addEditRubricRow(item.criterion || item.Criterion || '', item.maxPoints ?? item.MaxPoints ?? 1, item.description || item.Description || '');
                });

                if (result.totalPoints && result.totalPoints > 0) {
                    document.getElementById('editPoints').value = result.totalPoints;
                }
            } else {
                alert('Could not generate rubric: ' + (result.message || 'Unknown error'));
            }
        })
        .catch(err => {
            if (overlay) overlay.style.setProperty('display', 'none', 'important');
            if (btn) btn.disabled = false;
            alert('Error generating rubric: ' + err.message);
            console.error(err);
        });
    }

    function generateRubricWithAI() {
        const questionText = document.getElementById('newQuestionText').value.trim();
        const refAnswer = document.getElementById('newCorrectAnswer').value.trim();

        if (!questionText) {
            alert('Please fill in the Question Text first before generating a rubric.');
            return;
        }
        if (!refAnswer) {
            alert('Please fill in the Reference Correct Answer before generating a rubric. The AI needs the model answer to determine mark allocations.');
            return;
        }

        const pointsInput = document.getElementById('newPoints');
        const userPoints = pointsInput ? (parseInt(pointsInput.value, 10) || 0) : 0;

        const overlay = document.getElementById('rubricOverlay');
        const btn = document.getElementById('btnGenerateRubric');

        if (overlay) overlay.style.setProperty('display', 'flex', 'important');
        if (btn) btn.disabled = true;

        fetch('/QuizStudio/GenerateRubric', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                questionText: questionText,
                referenceAnswer: refAnswer,
                referenceAnswerImageUrl: scannedAnswerDiagramBase64,
                questionImageUrl: scannedDiagramBase64,
                points: userPoints
            })
        })
        .then(async r => {
            if (r.status === 401 || (r.redirected && r.url.includes('/Account/Login'))) {
                window.location.href = '/Account/Login';
                return { success: false, message: 'Session expired. Redirecting to login...' };
            }
            const text = await r.text();
            try {
                return JSON.parse(text);
            } catch(e) {
                return { success: false, message: 'Server error: ' + text.substring(0, 200) };
            }
        })
        .then(result => {
            if (overlay) overlay.style.setProperty('display', 'none', 'important');
            if (btn) btn.disabled = false;

            if (result.success && result.criteria && result.criteria.length > 0) {
                const container = document.getElementById('rubricTableBody');
                container.innerHTML = '';

                result.criteria.forEach(item => {
                    const row = document.createElement('div');
                    row.className = 'row align-items-center mb-3 mb-md-2 pb-3 pb-md-0 border-bottom border-secondary border-opacity-25 rubric-row';
                    row.innerHTML = `
                        <div class="col-12 col-md-3 mb-2 mb-md-0">
                            <label class="form-label d-md-none text-muted small mb-1">Criterion</label>
                            <input type="text" class="form-control form-control-sm rubric-criterion" value="${escapeHtml(item.criterion || item.Criterion || '')}" placeholder="Criterion name" />
                        </div>
                        <div class="col-12 col-md-2 mb-2 mb-md-0">
                            <label class="form-label d-md-none text-muted small mb-1">Max Pts</label>
                            <input type="number" class="form-control form-control-sm rubric-points" value="${item.maxPoints ?? item.MaxPoints ?? 1}" min="0" />
                        </div>
                        <div class="col-12 col-md-6 mb-2 mb-md-0">
                            <label class="form-label d-md-none text-muted small mb-1">Description</label>
                            <input type="text" class="form-control form-control-sm rubric-desc" value="${escapeHtml(item.description || item.Description || '')}" placeholder="Description" />
                        </div>
                        <div class="col-12 col-md-1 text-end text-md-center">
                            <button type="button" class="btn btn-sm btn-outline-danger w-100 w-md-auto" onclick="window.EdCoQuizEditor.removeRubricRow(this)"><i class="fa-solid fa-times"></i> <span class="d-md-none ms-1">Remove</span></button>
                        </div>
                    `;
                    container.appendChild(row);
                });

                if (result.totalPoints && result.totalPoints > 0) {
                    document.getElementById('newPoints').value = result.totalPoints;
                }
            } else {
                alert('Could not generate rubric: ' + (result.message || 'Unknown error'));
            }
        })
        .catch(err => {
            if (overlay) overlay.style.setProperty('display', 'none', 'important');
            if (btn) btn.disabled = false;
            alert('Error generating rubric: ' + err.message);
            console.error(err);
        });
    }

    function saveEditQuestion() {
        const id = parseInt(document.getElementById('editQuestionId').value, 10);
        const typeStr = document.getElementById('editQuestionType').value;
        const type = parseInt(typeStr, 10);

        let rubricJsonStr = "";
        if (type !== 0) {
            rubricJsonStr = getEditRubricJson();
        }

        const data = {
            id: id,
            questionText: document.getElementById('editQuestionText').value,
            questionType: type,
            points: parseInt(document.getElementById('editPoints').value, 10) || 1,
            optionA: document.getElementById('editOptionA').value,
            optionB: document.getElementById('editOptionB').value,
            optionC: document.getElementById('editOptionC').value,
            optionD: document.getElementById('editOptionD').value,
            correctOption: document.getElementById('editCorrectOption').value,
            correctAnswer: document.getElementById('editCorrectAnswer').value,
            correctAnswerImageUrl: editScannedAnswerDiagramBase64,
            rubricJson: rubricJsonStr,
            imageUrl: editScannedDiagramBase64
        };

        if (!data.questionText) {
            alert('Please fill in the question text.');
            return;
        }

        if (type === 0 && (!data.optionA || !data.optionB)) {
            alert('Please fill in at least options A and B for Multiple Choice.');
            return;
        }

        if (type !== 0 && (!data.rubricJson || data.rubricJson === "[]")) {
            alert('Please define at least one grading criterion in the rubric for the AI.');
            return;
        }

        fetch('/QuizStudio/UpdateQuestion', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
        .then(r => r.json())
        .then(result => {
            if (result.success) {
                location.reload();
            } else {
                alert('Error updating question: ' + (result.message || 'Unknown error'));
            }
        })
        .catch(err => {
            alert('Error updating question: ' + err.message);
        });
    }

    function sanitizeLaTeXText(input) {
        if (!input) return '';
        let text = input;

        text = text.replace(/\\r\\n/g, "\n").replace(/\\n/g, "\n").replace(/\r/g, "");
        text = text.replace(/(?:\\t|\t)+/g, " ");
        text = text.replace(/\\ /g, " ");
        text = text.replace(/\\{2,}(\(|\)|\[|\]|frac|sqrt|theta|pi|alpha|beta|degree|times|div|pm|begin|end|sin|cos|tan|array|tabular|matrix|pmatrix|bmatrix|hline)/g, "\\$1");

        const linesArr = text.split('\n');
        for (let i = 0; i < linesArr.length; i++) {
            let l = linesArr[i];
            if (/\\\(|\\\)|\\\[|\\\]/.test(l)) {
                l = l.replace(/\$/g, "");
            } else {
                l = l.replace(/\$\$([\s\S]*?)\$\$/g, "\\[$1\\]");
                l = l.replace(/(?<!\\)\$([^\$\n]+?)(?<!\\)\$/g, "\\($1\\)");
            }
            linesArr[i] = l;
        }
        text = linesArr.join('\n');

        text = text.replace(/\b([A-Za-z0-9]+)\s*\\\((.*?)\\\)/g, "\\($1 $2\\)");
        text = text.replace(/\b([A-Za-z0-9]+)_([0-9A-Za-z]+)\b/g, "$1_{$2}");

        const parts = text.split(/(\\\[[\s\S]*?\\\]|\\\([\s\S]*?\\\))/);
        text = parts.map(seg => {
            if (seg.startsWith('\\(') || seg.startsWith('\\[')) return seg;
            let wrapped = seg.replace(/\b([A-Za-z0-9]+_\{[A-Za-z0-9]+\})/g, "\\($1\\)");
            wrapped = wrapped.replace(/(?<!\\\()(\\(?:frac|sqrt|sin|cos|tan)\{[^{}]*\}(?:\{[^{}]*\})?)/g, "\\($1\\)");
            return wrapped;
        }).join('');

        text = text.replace(/\\\[(\d+)\\\]/g, "[$1]");

        const lines = text.split('\n');
        for (let i = 0; i < lines.length; i++) {
            let line = lines[i];

            const openBrace = (line.match(/\{/g) || []).length;
            const closeBrace = (line.match(/\}/g) || []).length;
            if (openBrace > closeBrace) {
                line += '}'.repeat(openBrace - closeBrace);
            }

            const openParen = (line.match(/\\\(/g) || []).length;
            const closeParen = (line.match(/\\\)/g) || []).length;
            if (openParen > closeParen) {
                line += ' ' + '\\)'.repeat(openParen - closeParen);
            }

            const openBracket = (line.match(/\\\[/g) || []).length;
            const closeBracket = (line.match(/\\\]/g) || []).length;
            if (openBracket > closeBracket) {
                line += ' ' + '\\]'.repeat(openBracket - closeBracket);
            }

            lines[i] = line;
        }
        text = lines.join('\n');

        text = text.replace(/[ \t]{2,}/g, " ");
        text = text.replace(/\\\(\s+/g, "\\(");
        text = text.replace(/\s+\\\)/g, "\\)");

        return text.trim();
    }

    function sanitizeExtractedText(text) {
        if (!text) return '';
        let cleaned = text.trim();
        if (cleaned.startsWith('{')) {
            try {
                const parsed = JSON.parse(cleaned);
                const result = parsed.questionText || parsed.answerText || parsed.referenceAnswer || parsed.correctAnswer || parsed.answer;
                if (result) cleaned = result;
            } catch(e) {
                const match = cleaned.match(/"(?:questionText|answerText|referenceAnswer|correctAnswer|answer)"\s*:\s*"([\s\S]*?)"(?:\s*,|\s*\})/);
                if (match && match[1]) cleaned = match[1].replace(/\\n/g, "\n").replace(/\\"/g, '"').replace(/\\\\/g, "\\");
            }
        }
        return sanitizeLaTeXText(cleaned);
    }

    // Attach DOM event listeners
    document.addEventListener('DOMContentLoaded', function () {
        updateQuestionPreview();
        updateAnswerPreview();
        if (window.MathJax && window.MathJax.typesetPromise) {
            window.MathJax.typesetPromise();
        }

        const cropModal = document.getElementById('cropModal');
        if (cropModal) {
            cropModal.addEventListener('shown.bs.modal', function () {
                const backdrops = document.querySelectorAll('.modal-backdrop');
                if (backdrops.length > 1) {
                    backdrops[backdrops.length - 1].style.zIndex = '1080';
                }

                const cropImage = document.getElementById('cropImage');
                if (cropper) {
                    cropper.destroy();
                    cropper = null;
                }
                if (typeof Cropper !== 'undefined') {
                    cropper = new Cropper(cropImage, {
                        viewMode: 2,
                        dragMode: 'crop',
                        autoCropArea: 1,
                        responsive: true,
                        checkOrientation: true,
                        background: true,
                        guides: true,
                        center: true,
                        cropBoxMovable: true,
                        cropBoxResizable: true
                    });
                }
            });

            cropModal.addEventListener('hidden.bs.modal', function () {
                if (cropper) {
                    cropper.destroy();
                    cropper = null;
                }
                document.getElementById('cropImage').src = '';
                if (document.getElementById('editQuestionModal').classList.contains('show')) {
                    document.body.classList.add('modal-open');
                }
            });
        }

        const editQuestionModal = document.getElementById('editQuestionModal');
        if (editQuestionModal) {
            editQuestionModal.addEventListener('shown.bs.modal', function () {
                updateEditQuestionPreview();
                updateEditAnswerPreview();
            });
        }

        const btnCropConfirm = document.getElementById('btnCropConfirm');
        if (btnCropConfirm) {
            btnCropConfirm.addEventListener('click', function() {
                if (!cropper) return;

                const canvas = cropper.getCroppedCanvas({ maxWidth: 1024, maxHeight: 1024 });
                if (!canvas) {
                    alert('Please draw a crop area on the image first.');
                    return;
                }

                const base64Image = canvas.toDataURL('image/jpeg', 0.8);

                const modal = bootstrap.Modal.getInstance(document.getElementById('cropModal'));
                if (modal) modal.hide();

                if (currentScanTarget === 'diagram') {
                    scannedDiagramBase64 = base64Image;
                    document.getElementById('diagramPreview').src = base64Image;
                    document.getElementById('diagramPreviewContainer').style.display = 'block';
                    document.getElementById('btnRemoveDiagram').style.display = 'inline-block';
                    return;
                }

                if (currentScanTarget === 'answer_diagram') {
                    scannedAnswerDiagramBase64 = base64Image;
                    document.getElementById('answerDiagramPreview').src = base64Image;
                    document.getElementById('answerDiagramPreviewContainer').style.display = 'block';
                    document.getElementById('btnRemoveAnswerDiagram').style.display = 'inline-block';
                    return;
                }

                if (currentScanTarget === 'edit_diagram') {
                    editScannedDiagramBase64 = base64Image;
                    document.getElementById('editDiagramPreview').src = base64Image;
                    document.getElementById('editDiagramPreviewContainer').style.display = 'block';
                    document.getElementById('btnRemoveEditDiagram').style.display = 'inline-block';
                    return;
                }

                if (currentScanTarget === 'edit_answer_diagram') {
                    editScannedAnswerDiagramBase64 = base64Image;
                    document.getElementById('editAnswerDiagramPreview').src = base64Image;
                    document.getElementById('editAnswerDiagramPreviewContainer').style.display = 'block';
                    document.getElementById('btnRemoveEditAnswerDiagram').style.display = 'inline-block';
                    return;
                }

                let activeOverlayId = 'scanOverlay';
                if (currentScanTarget === 'answer') activeOverlayId = 'scanAnswerOverlay';
                else if (currentScanTarget === 'edit_question') activeOverlayId = 'scanEditOverlay';
                else if (currentScanTarget === 'edit_answer') activeOverlayId = 'scanEditAnswerOverlay';

                const activeOverlay = document.getElementById(activeOverlayId);
                if (activeOverlay) {
                    activeOverlay.style.setProperty('display', 'flex', 'important');
                }

                const apiTarget = (currentScanTarget === 'answer' || currentScanTarget === 'edit_answer') ? 'answer' : 'question';
                fetch('/QuizStudio/ExtractTextFromImage', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ base64Image: base64Image, target: apiTarget, subjectName: currentSubjectName })
                })
                .then(async r => {
                    if (r.status === 401 || (r.redirected && r.url.includes('/Account/Login'))) {
                        window.location.href = '/Account/Login';
                        return { success: false, message: 'Session expired. Redirecting to login...' };
                    }
                    const text = await r.text();
                    try {
                        return JSON.parse(text);
                    } catch(e) {
                        return { success: false, message: 'Server returned unexpected response: ' + text.substring(0, 200) };
                    }
                })
                .then(result => {
                    if (activeOverlay) {
                        activeOverlay.style.setProperty('display', 'none', 'important');
                    }
                    if (result.success && result.text) {
                        const cleanExtractedText = sanitizeExtractedText(result.text);
                        if (currentScanTarget === 'answer') {
                            const currentVal = document.getElementById('newCorrectAnswer').value;
                            document.getElementById('newCorrectAnswer').value = (currentVal && currentVal.trim()) ? (currentVal + '\n' + cleanExtractedText) : cleanExtractedText;
                            updateAnswerPreview();
                        } else if (currentScanTarget === 'edit_answer') {
                            const currentVal = document.getElementById('editCorrectAnswer').value;
                            document.getElementById('editCorrectAnswer').value = (currentVal && currentVal.trim()) ? (currentVal + '\n' + cleanExtractedText) : cleanExtractedText;
                            updateEditAnswerPreview();
                        } else if (currentScanTarget === 'edit_question') {
                            const currentVal = document.getElementById('editQuestionText').value;
                            document.getElementById('editQuestionText').value = (currentVal && currentVal.trim()) ? (currentVal + '\n' + cleanExtractedText) : cleanExtractedText;
                            updateEditQuestionPreview();

                            const hasOptions = result.optionA || result.optionB || result.optionC || result.optionD;
                            if (hasOptions) {
                                const typeSelect = document.getElementById('editQuestionType');
                                typeSelect.value = '0';
                                toggleEditQuestionType();

                                document.getElementById('editOptionA').value = result.optionA || '';
                                document.getElementById('editOptionB').value = result.optionB || '';
                                document.getElementById('editOptionC').value = result.optionC || '';
                                document.getElementById('editOptionD').value = result.optionD || '';

                                if (result.correctOption) {
                                    const correctSelect = document.getElementById('editCorrectOption');
                                    const letter = result.correctOption.trim().toUpperCase();
                                    if (['A','B','C','D'].includes(letter)) {
                                        correctSelect.value = letter;
                                    }
                                }
                            }
                        } else {
                            const currentVal = document.getElementById('newQuestionText').value;
                            document.getElementById('newQuestionText').value = (currentVal && currentVal.trim()) ? (currentVal + '\n' + cleanExtractedText) : cleanExtractedText;
                            updateQuestionPreview();

                            const hasOptions = result.optionA || result.optionB || result.optionC || result.optionD;
                            if (hasOptions) {
                                const typeSelect = document.getElementById('newQuestionType');
                                typeSelect.value = '0';
                                toggleQuestionType();

                                document.getElementById('newOptionA').value = result.optionA || '';
                                document.getElementById('newOptionB').value = result.optionB || '';
                                document.getElementById('newOptionC').value = result.optionC || '';
                                document.getElementById('newOptionD').value = result.optionD || '';

                                if (result.correctOption) {
                                    const correctSelect = document.getElementById('newCorrectOption');
                                    const letter = result.correctOption.trim().toUpperCase();
                                    if (['A','B','C','D'].includes(letter)) {
                                        correctSelect.value = letter;
                                    }
                                }
                            }
                        }
                    } else {
                        alert('Could not extract text: ' + (result.message || 'Unknown error'));
                    }
                })
                .catch(err => {
                    if (activeOverlay) {
                        activeOverlay.style.setProperty('display', 'none', 'important');
                    }
                    alert('Error extracting text: ' + err.message);
                    console.error(err);
                });
            });
        }
    });

    // Expose public module API to window scope for inline event attributes (e.g. onclick="window.EdCoQuizEditor.addQuestion()")
    window.EdCoQuizEditor = {
        toggleQuestionType,
        addRubricRow,
        removeRubricRow,
        addQuestion,
        deleteQuestion,
        updateExamStatus,
        updateQuizTitle,
        triggerScan,
        removeDiagramImage,
        removeAnswerDiagramImage,
        processCameraImage,
        editQuestion,
        toggleEditQuestionType,
        addEditRubricRow,
        removeEditRubricRow,
        removeEditDiagramImage,
        removeEditAnswerDiagramImage,
        updateEditQuestionPreview,
        updateEditAnswerPreview,
        generateEditRubricWithAI,
        saveEditQuestion,
        generateRubricWithAI,
        updateQuestionPreview,
        updateAnswerPreview
    };

    // Also attach global aliases for backwards compatibility with inline onclick handlers
    window.toggleQuestionType = toggleQuestionType;
    window.addRubricRow = addRubricRow;
    window.removeRubricRow = removeRubricRow;
    window.addQuestion = addQuestion;
    window.deleteQuestion = deleteQuestion;
    window.updateExamStatus = updateExamStatus;
    window.updateQuizTitle = updateQuizTitle;
    window.triggerScan = triggerScan;
    window.removeDiagramImage = removeDiagramImage;
    window.removeAnswerDiagramImage = removeAnswerDiagramImage;
    window.processCameraImage = processCameraImage;
    window.editQuestion = editQuestion;
    window.toggleEditQuestionType = toggleEditQuestionType;
    window.addEditRubricRow = addEditRubricRow;
    window.removeEditRubricRow = removeEditRubricRow;
    window.removeEditDiagramImage = removeEditDiagramImage;
    window.removeEditAnswerDiagramImage = removeEditAnswerDiagramImage;
    window.updateEditQuestionPreview = updateEditQuestionPreview;
    window.updateEditAnswerPreview = updateEditAnswerPreview;
    window.generateEditRubricWithAI = generateEditRubricWithAI;
    window.saveEditQuestion = saveEditQuestion;
    window.generateRubricWithAI = generateRubricWithAI;
    window.updateQuestionPreview = updateQuestionPreview;
    window.updateAnswerPreview = updateAnswerPreview;
})();
