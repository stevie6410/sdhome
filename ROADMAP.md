# SDHome Signals - Feature Roadmap

> A self-hosted, self-sufficient home automation system for Zigbee and WiFi devices.
> **Goals**: Measure, automate, and visualize your home with a slick, robust, futuristic, integrated experience.

---

## 🏗️ Architecture Overview

### Hybrid Automation Strategy

SDHome uses a three-layer automation architecture for maximum flexibility and intelligence:

```
┌─────────────────────────────────────────────────────────────┐
│                      SDHome.Api                              │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐ │
│  │  Rule Engine    │  │  AI Agent       │  │  n8n        │ │
│  │  (Native .NET)  │  │  (Semantic      │  │  (External  │ │
│  │                 │  │   Kernel)       │  │   webhooks) │ │
│  │  • Fast rules   │  │                 │  │             │ │
│  │  • Schedules    │  │  • NL commands  │  │  • Complex  │ │
│  │  • Triggers     │  │  • Context-aware│  │    external │ │
│  │  • Conditions   │  │  • Learning     │  │    flows    │ │
│  └────────┬────────┘  └────────┬────────┘  └──────┬──────┘ │
│           │                    │                   │        │
│           ▼                    ▼                   ▼        │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Unified Action Executor                  │  │
│  │         (Device control, notifications, etc.)         │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

| Layer | Technology | Purpose | Response Time |
|-------|------------|---------|---------------|
| **Rule Engine** | Native .NET | Fast, reliable automations (90% of use cases) | <10ms |
| **AI Agent** | Semantic Kernel + Ollama | Natural language, context-aware decisions | 100ms-2s |
| **External Workflows** | n8n (optional) | Complex multi-service integrations | Variable |

### Tech Stack

| Component | Technology | Why |
|-----------|------------|-----|
| Rule Engine | Custom .NET | Fast, integrated, full control |
| AI/Agents | Semantic Kernel + Ollama | .NET native, local LLM option |
| Local LLM | Ollama (llama3, mistral) | Privacy, no cloud dependency |
| Voice | Whisper.cpp | Local speech-to-text |
| External Workflows | n8n (optional) | Complex integrations only |

---

## 🎨 Design System

### Visual Philosophy
**Futuristic • Minimal • Intelligent**

- Dark mode primary with glowing accent colors
- Glassmorphism panels with subtle blur and transparency
- Micro-animations that feel responsive and alive
- Progressive disclosure - simple by default, power when needed
- AI as a companion, not a gimmick

### Color Palette (Dark Mode)

| Element | Color | Usage |
|---------|-------|-------|
| Background | `#0a0a0f` | Main background |
| Surface | `#12121a` | Cards, panels |
| Surface Elevated | `#1a1a24` | Modals, dropdowns |
| Primary | `#00d4ff` | Actions, active states |
| Secondary | `#a855f7` | AI elements, highlights |
| Success | `#22c55e` | Confirmations, online |
| Warning | `#f59e0b` | Alerts, attention |
| Error | `#ef4444` | Errors, critical |
| Text Primary | `#ffffff` | Headings |
| Text Secondary | `#94a3b8` | Body, labels |

### Glassmorphism Style
```css
.glass-card {
  background: rgba(255, 255, 255, 0.05);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
}
```

### AI Visual Identity
- Purple/cyan gradient accent for AI elements
- Sparkle ✨ icon for AI-powered features
- Typing dots animation while AI thinks
- Subtle glow around AI-generated content

---

## 🎯 Current Sprint

_Features actively being worked on_

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| - | - | - | _Move items here when starting work_ |

---

## 📋 Backlog

### 🔌 Device Management & Integration

- [ ] **DM-001**: Zigbee Device Pairing via UI
  - Enable/disable pairing mode from Angular UI
  - Real-time device discovery notifications via SignalR
  - Publish to `zigbee2mqtt/bridge/request/permit_join`
  - Subscribe to `zigbee2mqtt/bridge/event` for join events
  - Pairing wizard with countdown timer
  - Device naming and room assignment on discovery

- [ ] **DM-002**: Device Discovery & Health Monitoring
  - Auto-discover new Zigbee/WiFi devices
  - Track last seen, signal strength, battery levels
  - Device offline alerts

- [ ] **DM-003**: Unified Device Abstraction Layer
  - Normalize Zigbee, WiFi (Tasmota, ESPHome, Shelly) into common model
  - Device capabilities system (switch, dimmer, sensor, thermostat, etc.)
  - Protocol-agnostic device control

- [ ] **DM-004**: Device Grouping & Rooms
  - Assign devices to rooms/zones
  - Room entity with floor/area hierarchy
  - Control groups (e.g., "All Living Room Lights")

- [ ] **DM-005**: WiFi Device Integration
  - Tasmota device discovery and control
  - ESPHome API integration
  - Shelly device support

---

### 📊 Measurement & Visualization

- [ ] **VIS-001**: Real-time Dashboard
  - Live sensor readings with SignalR
  - Customizable widget-based dashboard
  - Temperature, humidity, power usage widgets
  - Dark mode futuristic design

- [ ] **VIS-002**: Historical Charts
  - Time-series charts for sensor data
  - Time range selection (hour, day, week, month)
  - Comparison overlays (today vs yesterday)

- [ ] **VIS-003**: Energy Monitoring
  - Track power consumption per device/room
  - Daily/weekly/monthly aggregations
  - Cost calculations based on utility rates
  - Energy budget alerts

- [ ] **VIS-004**: Environmental Monitoring
  - Temperature/humidity by room
  - Air quality tracking (CO2, PM2.5)
  - Trend analysis and anomaly detection

- [ ] **VIS-005**: Floor Plan View
  - Interactive SVG floor plan
  - Drag-drop device placement
  - Visual device states on map
  - Room temperature heatmaps

- [ ] **VIS-006**: Enhanced Grafana Dashboards
  - Pre-built dashboards for common use cases
  - Prometheus metrics for all device states

---

### ⚡ Automation Engine

#### Layer 1: Native Rule Engine (Fast, Reliable)

- [ ] **AUTO-001**: Rule Engine Core
  - Rule data model: Triggers → Conditions → Actions
  - Trigger types: time, device state, sensor thresholds, sun position
  - Condition evaluation with AND/OR logic
  - Action execution with sequencing and delays
  - Rule enable/disable toggle
  - Execution history logging
  - **Target: <10ms execution time**

- [ ] **AUTO-002**: Visual Rule Builder UI
  - Drag-drop rule construction
  - WHEN (triggers) → IF (conditions) → THEN (actions) layout
  - Device/sensor picker with search
  - Real-time rule validation
  - Test/simulate rule before saving

- [ ] **AUTO-003**: Scenes
  - Save/restore device states as named scenes
  - Predefined scenes: "Movie Mode", "Good Night", "Away"
  - One-tap activation with visual feedback (ripple + glow)
  - Scene editing interface
  - Scene icons and colors

- [ ] **AUTO-004**: Schedules
  - Time-based automations with cron-like flexibility
  - Recurring schedules (daily, weekly, specific days)
  - Sunrise/sunset relative scheduling (e.g., "30 min before sunset")
  - Calendar view of scheduled events
  - Next run preview

- [ ] **AUTO-005**: Presence Detection
  - Home/away status per person
  - Network presence detection (phone on WiFi via ARP/DHCP)
  - Motion sensor integration
  - Arrival/departure automations
  - Presence history timeline

- [ ] **AUTO-006**: Sun Position Automation
  - Sunrise/sunset calculations for configured location
  - Solar elevation angle tracking
  - Blind/shade automation based on sun position
  - "Golden hour" triggers for lighting

#### Layer 2: AI Agent (Smart, Contextual)

- [ ] **AI-001**: Semantic Kernel Integration
  - Microsoft Semantic Kernel setup in .NET
  - Plugin architecture for device control, sensors, scenes
  - Ollama integration for local LLM (llama3, mistral)
  - Optional cloud LLM fallback (Azure OpenAI)

- [ ] **AI-002**: Natural Language Automation Builder
  - Text input: "Turn on porch light at sunset when someone is home"
  - AI parses → generates rule → user confirms
  - Voice input via Whisper.cpp (local speech-to-text)
  - Example suggestions and templates
  - "Edit manually" escape hatch to visual builder

- [ ] **AI-003**: AI Assistant Chat Interface
  - Slide-out panel or modal chat UI
  - Contextual queries: "What's using the most power?"
  - Voice command execution: "Turn on movie mode"
  - Conversation history within session
  - Action buttons in responses (e.g., "Create automation")

- [ ] **AI-004**: Smart Suggestions
  - Pattern detection: "You turn off lights at 11pm every night"
  - Proactive automation suggestions
  - Accept/customize/dismiss flow
  - Learning from user confirmations

- [ ] **AI-005**: Anomaly Detection
  - Unusual sensor readings detection
  - "Basement humidity spike - possible leak?"
  - Energy consumption anomalies
  - Device behavior changes

- [ ] **AI-006**: Context-Aware Decisions
  - Multi-factor reasoning: time, occupancy, weather, history
  - "It's too warm" → considers AC, windows, outside temp
  - Energy optimization recommendations
  - Forecast-aware scheduling

#### Layer 3: External Workflows (Optional)

- [ ] **AUTO-007**: n8n Integration (Simplified)
  - Webhook triggers from SDHome to n8n
  - n8n webhook callbacks to SDHome
  - Use only for complex external service integrations
  - Pre-built templates for common flows

---

### 🔔 Notifications & Alerts

- [ ] **NOTIF-001**: Multi-channel Notifications
  - Push notifications (Pushover, ntfy)
  - Email alerts
  - Telegram bot integration
  - Discord webhooks

- [ ] **NOTIF-002**: Alert Conditions
  - Temperature out of range
  - Device offline
  - Water leak detected
  - Door/window left open too long
  - Configurable thresholds

- [ ] **NOTIF-003**: Notification Preferences
  - Per-user notification settings
  - Quiet hours / Do Not Disturb
  - Alert severity levels

---

### 🎨 User Interface

- [ ] **UI-001**: Futuristic Design System
  - Dark mode with cyan/purple accent colors
  - Glassmorphism cards with backdrop blur
  - Micro-animations: ripples, pulses, glows
  - Consistent component library (buttons, cards, inputs)
  - Loading skeletons and transitions

- [ ] **UI-002**: Dashboard Home Screen
  - Contextual greeting based on time/weather
  - Room cards with live status (temp, lights, occupancy)
  - Quick scenes bar with one-tap activation
  - Energy summary widget with sparkline chart
  - Recent activity feed
  - Persistent AI input bar (text + voice)

- [ ] **UI-003**: Mobile-First PWA
  - Installable as app on phone
  - Offline capability for critical controls
  - Quick actions from home screen
  - Touch-optimized controls (large tap targets)
  - Bottom navigation for thumb reach

- [ ] **UI-004**: Device Control Widgets
  - Light dimmer sliders with smooth animation
  - Color picker for RGB lights (color wheel)
  - Thermostat control dial
  - Blind/shade position slider
  - Toggle switches with glow effect

- [ ] **UI-005**: Automations Hub
  - List of active automations with status
  - Last run time and success/failure indicator
  - AI suggestion cards inline
  - Tabs: Rules, Schedules, Scenes, History
  - Search and filter

- [ ] **UI-006**: Automation History Timeline
  - Chronological event feed
  - Trigger source and actions taken
  - Success/failure status with error details
  - AI insights inline
  - Filter by automation, device, or time

- [ ] **UI-007**: AI Assistant Panel
  - Slide-out or modal chat interface
  - Message bubbles with user/AI distinction
  - Action buttons in AI responses
  - Voice input button (microphone)
  - Typing indicator animation

- [ ] **UI-008**: Voice Control Ready
  - Whisper.cpp integration for local STT
  - Wake word detection (optional)
  - Visual feedback during listening
  - Command confirmation before execution

---

### 🔒 Security & Robustness

- [ ] **SEC-001**: Authentication & Access Control
  - User accounts with roles (admin, user, guest)
  - Device/room permissions
  - API key management for integrations
  - Session management

- [ ] **SEC-002**: Audit Logging
  - Track all device commands
  - Who triggered what and when
  - Timeline view of home events

- [ ] **SEC-003**: Backup & Restore
  - Database backup scheduling
  - Configuration export/import
  - Device state snapshots
  - One-click restore

- [ ] **SEC-004**: Local-First Architecture
  - Everything works without internet
  - Optional cloud sync for remote access
  - Local DNS/mDNS discovery

---

### 🔌 Integrations

- [ ] **INT-001**: Webhook Support (Enhanced)
  - Incoming webhooks for external triggers
  - Outgoing webhooks for actions
  - Webhook signature verification
  - Retry logic for failed webhooks

- [ ] **INT-002**: n8n Workflow Integration
  - Trigger n8n workflows from automations
  - n8n triggers for SDHome events
  - Pre-built workflow templates

- [ ] **INT-003**: Calendar Integration
  - Google Calendar sync
  - Outlook calendar sync
  - Vacation mode from calendar events
  - Meeting-aware automations

- [ ] **INT-004**: Weather Integration
  - Local weather data API
  - Weather-based automations
  - "Close blinds if sunny and hot"
  - Forecast-aware scheduling

- [ ] **INT-005**: Media Player Integration
  - Detect TV/media playback state
  - "Playing" state for movie mode
  - Volume control integration

---

## ✅ Completed

_Move completed features here with completion date_

| ID | Feature | Completed | Notes |
|----|---------|-----------|-------|
| - | Initial MQTT integration | ✅ | SignalsMqttWorker |
| - | Basic device tracking | ✅ | DeviceEntity, DeviceService |
| - | Signal event logging | ✅ | SignalEventEntity |
| - | Sensor readings storage | ✅ | SensorReadingEntity |
| - | Trigger events | ✅ | TriggerEventEntity |
| - | SignalR hub | ✅ | Real-time updates |
| - | Basic Angular UI | ✅ | Dashboard, devices, readings, triggers views |

---

## 📝 Notes

### Priority Guidelines
1. **Foundation first**: Device abstraction layer enables everything else
2. **Quick wins**: Real-time dashboard provides immediate value
3. **Core automation**: Native rule engine for reliable automations
4. **AI layer**: Add intelligence on top of solid foundation
5. **Polish**: UI/UX improvements and advanced features

### Recommended Build Order

| Phase | Focus | Features |
|-------|-------|----------|
| **Phase 1** | Foundation | DM-001, DM-003, UI-001, UI-002 |
| **Phase 2** | Core Automation | AUTO-001, AUTO-002, AUTO-003, UI-005 |
| **Phase 3** | Intelligence | AI-001, AI-002, AI-003, UI-007 |
| **Phase 4** | Polish | VIS-002, NOTIF-001, UI-003, AI-004 |
| **Phase 5** | Advanced | AI-005, AI-006, VIS-005, INT-003 |

### Technical Decisions
- Zigbee2MQTT as the Zigbee coordinator (no custom Zigbee stack)
- Mosquitto as MQTT broker (reliable, decoupled from API)
- MQTT as the message bus for all device communication
- SignalR for real-time UI updates
- EF Core direct access (no repository pattern)
- Angular standalone components
- Semantic Kernel for AI/agent capabilities
- Ollama for local LLM inference (privacy-first)

### AI Implementation Notes
- Start with Ollama + llama3 for local inference
- Design plugin architecture so AI can call device actions
- Keep rule engine as primary - AI is enhancement, not replacement
- Always show user what AI will do before executing
- Log all AI decisions for transparency

---

_Last updated: 2025-11-30_
