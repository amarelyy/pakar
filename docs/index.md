---
layout: default
---

# PAKAR (Parkir Akurat Real Time)

---

## Group 2 Members
1. Ursula Maurentti Amarely - 24/533008/TK/59050
2. Deva Zukannada - 24/546873/TK/60780
3. Nabil Abrian Aryo Prabowo - 24/546496/TK/60770

---

# IT Senior Project
Welcome to our IT Senior Project showcase page.

## Project Background
Limited parking capacity can cause people to spend a significant amount of time searching for an available parking space. This can also lead to congestion around parking areas and make the overall parking experience less convenient.

People often have to go through long lines of waiting outside of the mall or in a university parking area, then go out because the parking spot is full. PAKAR is created to monitor the availability of parking space.
Therefore, our group proposes an Al-based Smart Parking Web Application that provides parking availability information and predicts parking slot availability behavior over time. The system is designed to help people make better decisions about where and when to park.

## Methodology
Agile (Scrum Framework) is used as our methodology, why?
- **Short Timelines:** The project runs across 12 sessions in one semester. Agile helps the team build, test, and release features in small cycles.
- **AI Model Improvement:** Building computer vision models for slot detection and AI forecasting requires continuous testing and model adjustments.
- **Flexibility:** It allows quick changes if the team runs into issues during Azure cloud deployment or CCTV camera integration.

## SDLC Phase 1-3 Design
### a. Product Goals
- Track parking space availability in real time using existing CCTV cameras instead of buying expensive hardware sensors.
- Predict future parking space availability using AI models so drivers can plan ahead.
- Provide parking managers with historical usage data and charts to manage parking spaces better.

### b. Potential Product Users
**End-user (Students, Lecturers, Staff)** 
- Want to check real-time parking spot availability on a web dashboard.
- Need predictions for available spots during peak campus hours.
- Want alerts when a parking lot is completely full.

**Administrators**
- Need an affordable parking tracking system using existing CCTV cameras.
- Want to view historical parking usage data through simple charts.
- Need warning alerts when parking spaces reach full capacity.

### c. Use Case Diagram
![UseCase](img_usecase.png)

### d. Functional Requirements
| **FR** | **Description** |
|----|-------------|
| FR1 | The system shall display the current occupancy count, or an occupied/available status where an exact count is unavailable, for each parking zone within 5 seconds of a status change. |
| FR2 | The system shall return filtered zone results (by building proximity and availability) within 2 seconds of a user submitting a search query. |
| FR3 | The system shall allow a guest (non-logged-in) user to view real-time zone status within 1 interaction from app launch. |
| FR4 | The system shall suggest the nearest available alternate zone within 3 seconds when a user's selected zone is marked full. |
| FR5 | The system shall allow a user to submit an inaccurate-occupancy report within 3 interactions from the map view. |
| FR6 | The system shall notify the user of the report's submission status (received/pending review) immediately after submission. |
| FR7 | The system shall allow a user to create an account using name, email, and password. |
| FR8 | The system shall allow a registered user to log in using their email and password. |
| FR9 | The system shall restrict zone/camera/user management functions to accounts with the admin role. |
| FR10 | The system shall update a parking spot's status upon receiving a new detection result from its associated camera. |
| FR11 | The system shall recalculate a zone's occupancy count whenever any spot within that zone changes status. |
| FR12 | The system shall flag a camera as inactive if no detection has been received from it within a defined timeout period (e.g. 2 minutes). |
| FR13 | The system shall allow an admin to add, edit, or remove a parking zone. |
| FR14 | The system shall allow an admin to add, edit, or remove a camera and assign it to a zone. |
| FR15 | The system shall allow an admin to view, edit, or deactivate any user account. |
| FR16 | The system shall allow an admin to review submitted inaccuracy reports and mark them as resolved or dismissed. |
| FR17 | The system shall allow an admin to manually override a zone's displayed status when flagged as inaccurate or during camera downtime. |

### e. Entity Relationship Diagram
![ERD](img_ERD.png)

### f. Low-Fidelity Wireframe
User Interface
![lofi1](img_lofi1.png)
![lofi2](img_lofi2.png)
![lofi3](img_lofi3.png)
Admin Interface
![lofi4](img_lofi4.png)

### g. Gantt-Chart of Project Execution over 1 semester
![timeline](img_timeline.png)
